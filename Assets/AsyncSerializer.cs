using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using UnityEngine;

public class AsyncSerializer : MonoBehaviour
{
    [System.Serializable, DataContract]
    [KnownType(typeof(SaveValue))]
    public struct SaveValue
    {
        string key;
        SaveValues value;
        public SaveValue(string key, int value)
        {
            this.key = key;
            this.value = new SaveValues() { alpha = value, beta = value, gamma = (char)value };
        }
        [DataMember]
        public string Key => key;
        [DataMember]
        public SaveValues Value => value;
    }

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public double alpha;
        [DataMember] public int beta;
        [DataMember] public char gamma;
    }

    IEnumerator Start()
    {
        var list = new List<SaveValue>();
        list.Add(new SaveValue("1", 1));
        list.Add(new SaveValue("2", 2));
        list.Add(new SaveValue("3", 3));
        list.Add(new SaveValue("4", 4));
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
