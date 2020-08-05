using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Xbox.Services.ConnectedStorage;
using Windows.Gaming.XboxLive.Storage;

#if ENABLE_WINMD_SUPPORT
using Windows.System;
using Windows.Storage.Streams;
#endif

using UnityEngine;

public abstract class XboxLiveGameSaveContainer<T> where T : class
{
    public abstract string ContainerName { get; }
    public abstract string DisplayContainerName { get; }
    public XboxLiveUserHandler Handler { private set; get; }
    public GameSaveProvider Provider => Handler.GameSave;
    public XboxLiveGameSaveContainer(XboxLiveUserHandler handler)
    {
        Handler = handler;
    }

    protected IEnumerable<string> GetSerializableFields()
    {
        Type type = typeof(T);
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
        foreach (var f in fields)
        {
            if (f.IsNotSerialized) continue;
            yield return f.Name;
        }
    }
    protected virtual IEnumerable<KeyValuePair<string, byte[]>> SerializeData()
    {
        Type type = typeof(T);
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
        foreach (var f in fields)
        {
            if (f.IsNotSerialized) continue;
            string name = f.Name;
            Type ft = f.FieldType;
            object obj = f.GetValue(this);
            MethodInfo method = typeof(BitConverter).GetMethod("GetBytes", new Type[] { ft });
            if (method != null)
            {
                byte[] buff = (byte[])method.Invoke(null, new object[] { obj });
                yield return new KeyValuePair<string, byte[]>(name, buff);
            }
            else
            {
                XboxLiveUserManager.Log(XboxLiveLogType.Warning, string.Format("{0} with type {1} is not supported.", name, ft));
            }
        }
    }
    protected virtual void DeserializeData(KeyValuePair<string, byte[]>[] buffer)
    {
        XboxLiveUserManager.Log(XboxLiveLogType.Normal, "Deserializing data..." + buffer.Length);
        Type type = typeof(T);
        try
        {
            foreach (var v in buffer)
            {
                FieldInfo fi = type.GetField(v.Key);
                if (fi != null)
                {
                    object value = null;
                    var ft = fi.FieldType;
                    if (ft == typeof(int)) value = BitConverter.ToInt32(v.Value, 0);
                    else if (ft == typeof(long)) value = BitConverter.ToInt64(v.Value, 0);
                    else if (ft == typeof(short)) value = BitConverter.ToInt16(v.Value, 0);
                    else if (ft == typeof(uint)) value = BitConverter.ToUInt32(v.Value, 0);
                    else if (ft == typeof(ulong)) value = BitConverter.ToUInt64(v.Value, 0);
                    else if (ft == typeof(ushort)) value = BitConverter.ToUInt16(v.Value, 0);
                    else if (ft == typeof(string)) value = BitConverter.ToString(v.Value, 0);
                    else if (ft == typeof(double)) value = BitConverter.ToDouble(v.Value, 0);
                    else if (ft == typeof(float)) value = BitConverter.ToSingle(v.Value, 0);
                    else if (ft == typeof(bool)) value = BitConverter.ToBoolean(v.Value, 0);

                    fi.SetValue(this, value);
                    XboxLiveUserManager.Log(XboxLiveLogType.Normal, "= " + value);
                }
            }
        }
        catch (Exception e)
        {
            XboxLiveUserManager.Log(XboxLiveLogType.Error, e.Message);
        }
    }

    async Task<GameSaveStatus> _SaveData()
    {
#if ENABLE_WINMD_SUPPORT
        GameSaveContainer container = Provider.CreateContainer(ContainerName);

        Dictionary<string, IBuffer> dict = SerializeData().ToDictionary( 
        x => x.Key,
        (x)=>
        {
            using(DataWriter writer = new DataWriter())
            {
                writer.WriteBytes(x.Value);
                return writer.DetachBuffer();
            }
        });
        XboxLiveUserManager.Log(XboxLiveLogType.Normal, "Serializing data..." + dict.Count);
        GameSaveOperationResult result = await container.SubmitUpdatesAsync(dict, null, DisplayContainerName);
        return (GameSaveStatus)result.Status;
#else
        return GameSaveStatus.Ok;
#endif
    }
    async Task<GameSaveStatus> _LoadGame()
    {
#if ENABLE_WINMD_SUPPORT
        GameSaveContainer container = Provider.CreateContainer(ContainerName);

        string[] blobsToRead = GetSerializableFields().ToArray();

        // GetAsync allocates a new Dictionary to hold the retrieved data. You can also use ReadAsync
        // to provide your own preallocated Dictionary.
        GameSaveBlobGetResult result = await container.GetAsync(blobsToRead);

        int loadedData = 0;

        if(result.Status == GameSaveErrorStatus.Ok)
        {           
            Dictionary<string, byte[]> data = result.Value.ToDictionary(
                k => k.Key, 
                (k)=>
                {
                    using(DataReader dataReader = DataReader.FromBuffer(k.Value))
                    {
                        byte[] bytes = new byte[k.Value.Length];
                        dataReader.ReadBytes(bytes);
                        return bytes;
                    }
                });
            DeserializeData(data.ToArray());
        }

        return (GameSaveStatus)result.Status;    
#else
        return GameSaveStatus.Ok;
#endif
    }

    async Task<GameSaveStatus> _DeleteContainer()
    {
#if ENABLE_WINMD_SUPPORT
        GameSaveOperationResult result = await Provider.DeleteContainerAsync(ContainerName);
        return (GameSaveStatus)result.Status;
#else
        return GameSaveStatus.Ok;
#endif
    }
    public async void SaveGame(Action<GameSaveStatus> ondone)
    {
        try
        {
            GameSaveStatus status = await _SaveData();
            if (ondone != null) ondone(status);
        }
        catch (Exception e)
        {
            XboxLiveUserManager.Log(XboxLiveLogType.Error, e.Message);
            ondone(GameSaveStatus.NoAccess);
        }
    }
    public async void LoadGame(Action<GameSaveStatus> ondone)
    {
        try
        {
            GameSaveStatus status = await _LoadGame();
            if (ondone != null) ondone(status);
        }
        catch (Exception e)
        {
            XboxLiveUserManager.Log(XboxLiveLogType.Error, e.Message);
            ondone(GameSaveStatus.NoAccess);
        }
    }

    public async void DeleteContainer(Action<GameSaveStatus> ondone)
    {
        try
        {
            GameSaveStatus status = await _DeleteContainer();
            if (ondone != null) ondone(status);
        }
        catch (Exception e)
        {
            XboxLiveUserManager.Log(XboxLiveLogType.Error, e.Message);
            ondone(GameSaveStatus.NoAccess);
        }
    }
}
