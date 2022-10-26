using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using UnityEngine;

[System.Serializable, DataContract]
public class Container : AsyncSerializer.IKeyValue
{
    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public double alpha;
        [DataMember] public int beta;
        [DataMember] public byte gamma;
        [DataMember] public List<string> list;
    }

    [DataMember]
    SaveValues value;

    public Container(int value)
    {
        this.value = new SaveValues() {
            alpha = value,
            beta = value,
            gamma = (byte)value,
            list = new List<string>()
        };
        for (int i = 0; i < value; ++i)
        {
            this.value.list.Add(i.ToString());
        }
    }

    public string Key => GetType().Name;
    public object Value => value;
}

public class AsyncSerializer : MonoBehaviour
{
    public interface IKeyValue
    {
        string Key {  get; }
        object Value {  get; }
    }

    [System.Serializable, DataContract]
    [KnownType(typeof(SaveValue))]
    public class SaveValue
    {
        public SaveValue(IKeyValue keyValue)
        {
            this.keyValue = keyValue;
        }
        IKeyValue keyValue;

        [DataMember]
        public string Key => keyValue.Key;
        [DataMember]
        public object Value => keyValue.Value;
    }

    IEnumerator Start()
    {
        var list = new List<SaveValue>();
        list.Add(new SaveValue(new Container(1)));
        list.Add(new SaveValue(new Container(2)));
        list.Add(new SaveValue(new Container(3)));
        list.Add(new SaveValue(new Container(4)));
        yield return null;
        var serialiser = new AsyncSerialization.DataContractSerializer(list.GetType());
        var settings = new XmlWriterSettings()
        {
            Indent = true,
            IndentChars = "\t",
            NamespaceHandling = NamespaceHandling.OmitDuplicates
        };
        string xml;
        using (var writer = new StringWriter())
        {
            using (var xwr = XmlWriter.Create(writer, settings))
            {
                foreach (var entry in serialiser.WriteObject(xwr, list))
                {
                    yield return null;
                }
            }
            xml = writer.ToString();
        }

        Debug.Log(xml);
    }
}
