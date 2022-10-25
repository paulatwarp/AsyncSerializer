using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using UnityEngine;

[System.Serializable, DataContract]
public class Container : AsyncSerializer.SaveValue
{
    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public double alpha;
        [DataMember] public int beta;
        [DataMember] public char gamma;
    }

    string key;
    SaveValues value;

    public Container(string key, int value)
    {
        this.key = key;
        this.value = new SaveValues() { alpha = value, beta = value, gamma = (char)value };
    }

    [DataMember]
    public override string Key => key;
    [DataMember]
    public override object Value => value;
}

public class AsyncSerializer : MonoBehaviour
{
    [System.Serializable, DataContract]
    [KnownType(typeof(SaveValue))]
    public abstract class SaveValue
    {
        [DataMember]
        public abstract string Key { get; }
        [DataMember]
        public abstract object Value { get; }
    }

    IEnumerator Start()
    {
        var list = new List<SaveValue>();
        list.Add(new Container("1", 1));
        list.Add(new Container("2", 2));
        list.Add(new Container("3", 3));
        list.Add(new Container("4", 4));
        yield return null;
        var serialiser = new AsyncSerialization.DataContractSerializer(list.GetType());
        var settings = new XmlWriterSettings()
        {
            Indent = true,
            IndentChars = "\t"
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
