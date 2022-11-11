using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using UnityEngine;


namespace My.Namespace
{
    [System.Serializable, DataContract]
    public class BoolAsData : AsyncSerializer.IKeyValue
    {
        public string Key => GetType().Name;
        public object Value => boolAsData;

        SaveValue boolAsData;

        [System.Serializable, DataContract]
        public class SaveValue
        {
            [DataMember] public bool test;
        }

        public BoolAsData(bool value)
        {
            boolAsData = new SaveValue();
            boolAsData.test = value;
        }
    }

    [DataContract(IsReference = true), KnownType(typeof(SaveName)), KnownType(typeof(OverrideName))]
    public abstract class SaveData
    {
        [DataMember] protected bool enable;
        [DataMember] public string name;
        [DataMember] public object [] list;

        protected abstract void Save(bool enable);
    }

    [System.Serializable, DataContract(IsReference = true)]
    public class SaveName : SaveData
    {
        public SaveName(bool enable)
        {
            Save(enable);
        }

        protected override void Save(bool enable)
        {
            this.enable = enable;
            this.list = new object[2];
            this.list[0] = null;
            this.list[1] = new ContainingClass.ContractType(1);
        }
    }

    [System.Serializable, DataContract]
    public class NonReferenceData
    {
        [DataMember] public ContainingClass.ContractType data;
        [DataMember] public bool test;
    }

    [System.Serializable]
    public struct CustomVector
    {
        public int x;
        public int y;
    }
}

[System.Serializable, DataContract(IsReference = true)]
public class OverrideName : My.Namespace.SaveData
{
    [DataMember] public List<object> anyReferences;

    public OverrideName(bool enable, My.Namespace.SaveData reference)
    {
        Save(enable);
        anyReferences = new List<object>();
        anyReferences.Add(reference);
    }

    protected override void Save(bool enable)
    {
        this.enable = enable;
    }
}

[System.Serializable, DataContract]
public class ArrayOfData: AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => values;

    IEnumerable<SaveValues> values;

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public My.Namespace.SaveData[] data;
    }

    public ArrayOfData(My.Namespace.SaveData reference)
    {
        var saveValues = new SaveValues[1];
        saveValues[0] = new SaveValues();
        saveValues[0].data = new My.Namespace.SaveData[2];
        saveValues[0].data[0] = new My.Namespace.SaveName(true);
        saveValues[0].data[1] = new OverrideName(true, reference);
        values = saveValues;
    }
}

[System.Serializable, DataContract]
public class ReferenceObject : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => values;

    IEnumerable<My.Namespace.SaveData> values;

    public ReferenceObject()
    {
        var saves = new My.Namespace.SaveData[1];
        var array = new ContainingClass.ContractType[1];
        array[0] = new ContainingClass.ContractType(1);
        saves[0] = new My.Namespace.SaveName(true)
        {
            name = "MyName",
            list = new object[] { array, "MyName", new NonContract(1), new ContainingClass.ContractType(2) }
        };
        values = saves;
    }

    public My.Namespace.SaveData GetReference()
    {
        foreach (var value in values)
        {
            return value;
        }
        return null;
    }
}

[System.Serializable, DataContract]
public class Reference : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => references;

    object [] references;

    public Reference(My.Namespace.SaveData reference)
    {
        this.references = new object[1];
        this.references[0] = reference;
    }
}

[System.Serializable, DataContract]
public struct CustomType
{
    [DataMember] public My.Namespace.CustomVector vector;
}

[System.Serializable, DataContract]
public class SaveCustomType : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => data;

    object [] data;

    public SaveCustomType(int x, int y)
    {
        data = new object [1];
        var custom = new CustomType();
        custom.vector.x = x;
        custom.vector.y = y;
        data[0] = custom;
    }
}

[System.Serializable, DataContract]
public enum EnumType
{
    [EnumMember(Value = "First")]
    First,
    [EnumMember(Value = "Second")]
    Second
}

public enum EnumNoContract
{
    FIRST,
    SECOND
}

[System.Serializable, DataContract]
public class InternalData
{
    int internalData;

    [DataMember] public int Data { get { return internalData; } internal set { internalData = value; } }
}

[System.Serializable, DataContract]
public class InternalSet : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => data;

    InternalData data;

    public InternalSet(int x)
    {
        data = new InternalData();
        data.Data = x;
    }
}

[System.Serializable, DataContract]
public class EnumValue : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => type;

    EnumType type;

    public EnumValue(EnumType type)
    {
        this.type = type;
    }
}

[System.Serializable, DataContract]
public class EnumValueNoContract : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => values;

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public List<Content> list;
    }

    [System.Serializable]
    public class Content
    {
        public EnumNoContract type;
        public bool alpha;
    }

    SaveValues values;

    public EnumValueNoContract(EnumNoContract type)
    {
        values = new SaveValues();
        values.list = new List<Content>();
        values.list.Add(new Content());
        values.list[0].type = type;
        values.list[0].alpha = true;
    }
}

[System.Serializable, DataContract]
public class ListOfList : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => values;

    SaveValues values;

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public List<List<bool>> flags;
    }

    public ListOfList()
    {
        values = new SaveValues();
        values.flags = new List<List<bool>>();
        values.flags.Add(new List<bool>());
        values.flags[0].Add(true);
    }
}

[System.Serializable, DataContract]
public class ArrayOfArray : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => values;

    SaveValues values;

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public bool[][] flags;
    }

    public ArrayOfArray()
    {
        values = new SaveValues();
        values.flags = new bool[1][];
        values.flags[0] = new bool[1];
        values.flags[0][0] = true;
    }
}

[System.Serializable, DataContract]
public class ListOfKeyValuePair : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => values;

    SaveValues values;

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public List<KeyValuePair<string, float>> list;
    }

    public ListOfKeyValuePair(float x)
    {
        values = new SaveValues();
        values.list = new List<KeyValuePair<string, float>>();
        values.list.Add(new KeyValuePair<string, float>(x.ToString(), x));
    }
}

[System.Serializable, DataContract]
public class ListOfString: AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => values;

    SaveValues values;

    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public List<string> list;
    }

    public ListOfString(float x)
    {
        values = new SaveValues();
        values.list = new List<string>();
        values.list.Add(x.ToString());
    }
}


[System.Serializable, DataContract]
public class Vector : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => new Vector3(1, 2, 3);
}

[System.Serializable, DataContract]
public class Vector2D : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => new Vector2Int(1, 2);
}

[System.Serializable, DataContract]
public class BoolAsString : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => false.ToString();
}

[System.Serializable, DataContract]
public class BoolAsObject : AsyncSerializer.IKeyValue
{
    public string Key => GetType().Name;
    public object Value => false;
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
    public NonContract(int i)
    {
        this.i = i;
        list = new List<string>();
        list.Add(null);
        array = new ContainingClass.ContractType[0];
    }
    public int i;
    public List<string> list;
    public ContainingClass.ContractType[] array;
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
        [DataMember] public CustomType customType;
        [DataMember] public double alpha;
        [DataMember] public float phi;
        [DataMember] public int beta;
        [DataMember] public byte gamma;
        [DataMember] public bool condition;
        [DataMember] public bool conditions;
        [DataMember] public List<string> list;
        [DataMember] public List<Vector3> vectors;
        [DataMember] public List<ContainingClass.ContractType> contracts;
        [DataMember] public object nil;
    }

    [DataMember]
    SaveValues value;

    public Container(int value)
    {
        this.value = new SaveValues(value)
        {
            vector = Vector3.one * value,
            customType = new CustomType(),
            alpha = 1.0 / (value + 1),
            phi = 1.0f / (value + 1),
            beta = value,
            gamma = (byte)value,
            condition = value != 0,
            conditions = value != 0,
            list = new List<string>(),
            vectors = new List<Vector3>(),
            contracts = new List<ContainingClass.ContractType>(),
            nil = null
        };
        this.value.customType.vector.x = value;
        this.value.customType.vector.y = value;
        for (int i = 0; i < value; ++i)
        {
            this.value.list.Add(i.ToString());
            this.value.vectors.Add(Vector3.one * i);
            this.value.contracts.Add(new ContainingClass.ContractType(i));
        }
    }

    public string Key => GetType().Name;
    public object Value => value;
}

public class ContainingClass
{
    [System.Serializable, DataContract]
    public class ContractType
    {
        public ContractType(int i)
        {
            anInt = i;
            cDataList = new List<OtherContainingClass.ContractMixedData>();
            cDataList.Add(new OtherContainingClass.ContractMixedData(i));
            cDataList.Add(new OtherContainingClass.ContractMixedData(i+1));
            bStringList = new List<string>();
            bStringList.Add(i.ToString());
        }

        [DataMember] public List<OtherContainingClass.ContractMixedData> cDataList;
        [DataMember] public int anInt;
        [DataMember] public List<string> bStringList;
    }
}

public class OtherContainingClass
{
    [System.Serializable, DataContract]
    [KnownType(typeof(ContractPlainData))]
    public class ContractMixedData
    {
        public ContractMixedData(int i)
        {
            text = i.ToString();
            list = new List<ContractPlainData>();
        }

        [DataMember] public string text;
        [DataMember] public List<ContractPlainData> list;
    }

    [System.Serializable, DataContract]
    public class ContractPlainData
    {
        public ContractPlainData(int i)
        {
            text = i.ToString();
        }

        [DataMember] public string text;
    }
}


[System.Serializable, DataContract]
public class AlphaTest : AsyncSerializer.IKeyValue
{
    [System.Serializable, DataContract]
    public class SaveData
    {
        [DataMember] public int gamma;
        [DataMember] public int beta;
        [DataMember] public int alpha;
    }

    SaveData data;

    public AlphaTest()
    {
        data = new SaveData();
        data.alpha = 1;
        data.beta = 2;
        data.gamma = 3;
    }

    public string Key => GetType().Name;
    public object Value => data;
}

[System.Serializable, DataContract]
public class BetaTest : AsyncSerializer.IKeyValue
{
    [System.Serializable, DataContract]
    public class SaveData
    {
        [DataMember] public int delta;
        [DataMember] public int epsilon;
        [DataMember] public int alpha;
    }

    SaveData data;

    public BetaTest()
    {
        data = new SaveData();
        data.delta = 4;
        data.alpha = 2;
        data.epsilon = 3;
    }

    public string Key => GetType().Name;
    public object Value => data;
}


[System.Serializable, DataContract]
public class ContainerList : AsyncSerializer.IKeyValue
{
    [System.Serializable, DataContract]
    public class SaveValues
    {
        [DataMember] public double aDouble;
        [DataMember] public byte aByte;
        [DataMember] public int anInt;
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
                aDouble = value,
                anInt = value,
                aByte = (byte)value,
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
        var reference = new ReferenceObject();
        var data = reference.GetReference();
        list.Add(new SaveValue(reference));
        list.Add(new SaveValue(new Reference(null)));
        list.Add(new SaveValue(new ArrayOfData(data)));
        list.Add(new SaveValue(new ReferenceObject()));
        list.Add(new SaveValue(new Reference(data)));
        list.Add(new SaveValue(new ContainerList()));
        list.Add(new SaveValue(new ContainerList(1)));
        list.Add(new SaveValue(new AlphaTest()));
        list.Add(new SaveValue(new BetaTest()));
        list.Add(new SaveValue(new Container(1)));
        list.Add(new SaveValue(new My.Namespace.BoolAsData(false)));
        list.Add(new SaveValue(new SaveCustomType(1, 2)));
        list.Add(new SaveValue(new ArrayOfArray()));
        list.Add(new SaveValue(new ListOfList()));
        list.Add(new SaveValue(new EnumValueNoContract(EnumNoContract.FIRST)));
        list.Add(new SaveValue(new ListOfString(1)));
        list.Add(new SaveValue(new ListOfKeyValuePair(1)));
        list.Add(new SaveValue(new InternalSet(1)));
        list.Add(new SaveValue(new BoolAsObject()));
        list.Add(new SaveValue(new Vector2D()));
        list.Add(new SaveValue(new EnumValue(EnumType.First)));
        list.Add(new SaveValue(new EnumValue(EnumType.Second)));
        list.Add(new SaveValue(new DictionaryData()));
        list.Add(new SaveValue(new EmptyArrayOfNonContract()));
        list.Add(new SaveValue(new ArrayOfInt()));
        list.Add(new SaveValue(new ArrayOfNull()));
        list.Add(new SaveValue(new BoolAsString()));
        list.Add(new SaveValue(new Vector()));
        list.Add(new SaveValue(new Container(0)));
        //list.Add(new SaveValue(new Container(2)));
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
        File.WriteAllText("new.xml", xml.ToString());
        Debug.Log(xml);
    }
}
