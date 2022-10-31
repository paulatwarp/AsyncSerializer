using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using UnityEngine;


[System.Serializable, DataContract]
public class Vector : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => new Vector3(1, 2, 3);
}

[System.Serializable, DataContract]
public class BoolAsString : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => false.ToString();
}

[System.Serializable, DataContract]
public class ArrayOfNull : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => arrayOfNull;

    List<Vector> arrayOfNull;

    public ArrayOfNull()
    {
        arrayOfNull = new List<Vector>();
        arrayOfNull.Add(null);
    }
}

[System.Serializable, DataContract]
public class ArrayOfInt: AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => arrayOfInt;

    List<int> arrayOfInt;

    public ArrayOfInt()
    {
        arrayOfInt = new List<int>();
        arrayOfInt.Add(1);
    }
}

[System.Serializable]
public class NonContract
{
    public int i;
}

[System.Serializable, DataContract]
public class EmptyArrayOfNonContract : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => saveValues;

    SaveValues saveValues;

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public List<NonContract> contracts;
    }

    public EmptyArrayOfNonContract()
    {
        saveValues = new SaveValues();
        saveValues.contracts = new List<NonContract>();
    }
}

[System.Serializable, DataContract]
public class DictionaryData : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => saveValues;

    SaveValues saveValues;

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public Dictionary<int, bool> entries;
    }

    public DictionaryData()
    {
        saveValues = new SaveValues();
        saveValues.entries = new Dictionary<int, bool>();
        saveValues.entries.Add(0, true);
        saveValues.entries.Add(1, false);
    }
}

[System.Serializable, DataContract]
public class Container : AsyncSerializer.IKeyValue
{
    [System.Serializable, DataContract]
    public class SaveValues
    {
        public SaveValues(int secret)
        {
            this.secret = secret;
        }

        [DataMember] int secret;
        [DataMember] public Vector3 vector;
        [DataMember] public double alpha;
        [DataMember] public float phi;
        [DataMember] public int beta;
        [DataMember] public byte gamma;
        [DataMember] public bool condition;
        [DataMember] public List<string> list;
        [DataMember] public List<Vector3> vectors;
        [DataMember] public List<ContractType> contracts;
        [DataMember] public object nil;
    }

    [DataMember]
    SaveValues value;

    public Container(int value)
    {
        this.value = new SaveValues(value)
        {
            vector = Vector3.one * value,
            alpha = 1.0 / (value + 1),
            phi = 1.0f / (value + 1),
            beta = value,
            gamma = (byte)value,
            condition = value != 0,
            list = new List<string>(),
            vectors = new List<Vector3>(),
            contracts = new List<ContractType>(),
            nil = null
        };
        for (int i = 0; i < value; ++i)
        {
            this.value.list.Add(i.ToString());
            this.value.vectors.Add(Vector3.one * i);
            this.value.contracts.Add(new ContractType(i));
        }
    }

    public string Key => GetType().Name;
    public object Value => value;
}

[System.Serializable, DataContract]
public class ContractType
{
    public ContractType(int i)
    {
        alpha = i;
    }

    [DataMember] public double alpha;
}

[System.Serializable, DataContract]
public class ContainerList : AsyncSerializer.IKeyValue
{
    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public double alpha;
        [DataMember] public byte gamma;
        [DataMember] public int beta;
    }

    SaveValues[] list = new SaveValues[0];

    public ContainerList()
    {
    }

    public ContainerList(int value)
    {
        list = new SaveValues[value];
        for (int i = 0; i < value; ++i)
        {
            list[i] = new SaveValues()
            {
                alpha = value,
                beta = value,
                gamma = (byte)value,
            };
        }
    }

    public string Key => GetType().Name;
    public object Value => list;
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
        public string Key { get { return keyValue.Key; } set { } } 
        [DataMember]
        public object Value { get { return keyValue.Value; } set { } }
    }

    IEnumerator Start()
    {
        var list = new List<SaveValue>();
        list.Add(new SaveValue(new DictionaryData()));
        list.Add(new SaveValue(new EmptyArrayOfNonContract()));
        list.Add(new SaveValue(new ArrayOfInt()));
        list.Add(new SaveValue(new ArrayOfNull()));
        list.Add(new SaveValue(new BoolAsString()));
        list.Add(new SaveValue(new Vector()));
        list.Add(new SaveValue(new ContainerList()));
        list.Add(new SaveValue(new ContainerList(1)));
        list.Add(new SaveValue(new Container(0)));
        list.Add(new SaveValue(new Container(1)));
        list.Add(new SaveValue(new Container(2)));
        yield return null;
        var serialiser = new DataContractSerializer(list.GetType());
        var asyncSerialiser = new AsyncSerialization.DataContractSerializer(list.GetType());
        var settings = new XmlWriterSettings()
        {
            Indent = true,
            IndentChars = "\t",
            NamespaceHandling = NamespaceHandling.OmitDuplicates
        };
        string xml;
        XmlSpy spy;
        using (var writer = new StringWriter())
        {
            using (var xwr = XmlWriter.Create(writer, settings))
            {
                spy = new XmlSpy(xwr, "serialise.log");
                try
                {
                    serialiser.WriteObject(spy, list);
                }
                catch (System.Exception exception)
                {
                    Debug.Log(exception.ToString());
                }
                spy.WriteLog();
            }
            xml = writer.ToString();
        }
        File.WriteAllText("original.xml", xml.ToString());
        Debug.Log(xml);

        using (var writer = new StringWriter())
        {
            using (var xwr = XmlWriter.Create(writer, settings))
            {
                spy.CheckLog(xwr);
                foreach (var entry in asyncSerialiser.WriteObject(spy, list))
                {
                    yield return null;
                }
            }
            xml = writer.ToString();
        }

        Debug.Log(xml);
    }
}
