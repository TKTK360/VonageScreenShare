using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// ScreenShareScript
/// </summary>
public class ScreenShareScript : MonoBehaviour
{
#region << Field >>

    public RawImage shareImage;

    protected CancellationTokenSource _tokenSource = new CancellationTokenSource();

    protected const string PipeName = "PIPE_APP_SHARE";

    protected byte[] _data;

    protected Texture2D _tex;

    protected bool IsRun = false;

    protected int _width;
    protected int _height;

    private object lockTest = new object();
    protected bool _isUpdate = false;

#endregion << Field >>

    /// <summary>
    /// Start
    /// </summary>
    async void Start()
    {
        await Task.Run(() =>
        {
            MemoryMappedFile sharedMemory = null;
            IsRun = true;

            while (IsRun)
            {
                try
                {
                    sharedMemory = MemoryMappedFile.OpenExisting(PipeName);
                    if (sharedMemory != null)
                    {
                        break;
                    }
                }
                catch
                {
                }
                Thread.Sleep(1000);
            }


            MemoryMappedViewAccessor accessor = null;

            while (IsRun)
            {
                if (accessor == null)
                {
                    accessor = sharedMemory.CreateViewAccessor();
                }

                if (accessor != null)
                {
                    lock (lockTest)
                    {
                        var offset = sizeof(int);
                        var size = accessor.ReadInt32(0);
                        var width = accessor.ReadInt32(offset);
                        var height = accessor.ReadInt32(offset * 2);

                        Debug.LogFormat("{0}x{1}, {2}", width, height, size);


                        if (_data == null || _data.Length < size)
                        {
                            _data = new byte[size];
                        }

                        accessor.ReadArray<byte>(offset * 3, _data, 0, size);

                        _width = width;
                        _height = height;
                    }

                    _isUpdate = true;
                }

                Thread.Sleep(100);
            }

            accessor.Dispose();
        });
    }


    /// <summary>
    /// OnDestroy
    /// </summary>
    private void OnDestroy()
    {
        IsRun = false;
    }


    /// <summary>
    /// Update
    /// </summary>
    void Update()
    {
        if (!_isUpdate)
        {
            return;
        }

        _isUpdate = false;

        lock (lockTest)
        {
            if (_tex == null)
            {
                InitTexture(_width, _height);
            }

            if (_tex != null)
            {
                _tex.LoadRawTextureData(_data);
                _tex.Apply();
            }
        }
    }


    /// <summary>
    /// InitTexture
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    protected void InitTexture(int width, int height)
    {
        _tex = new Texture2D(width, height, TextureFormat.BGRA32, false);

        if (shareImage != null)
        {
            shareImage.texture = _tex;
        }

    }
}
