namespace AsyncSerialization
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml;
    using System.Xml.Schema;
    using UnityEngine;

    public class DataContractSerializer
    {
        public const string Namespace = "http://schemas.datacontract.org/2004/07/";
        public const string CollectionsNamespace = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
        public const string SerializationNamespace = "http://schemas.microsoft.com/2003/10/Serialization/";
        public const string XsiTypeLocalName = "type";
        public const string XsiPrefix = "i";
        public const string SerPrefix = "z";
        public const string XmlnsPrefix = "xmlns";
        public const string XsiNilLocalName = "nil";
        public const string IdLocalName = "Id";
        public const string RefLocalName = "Ref";

        Type rootElementType;
        XmlWriter writer;
        int depth;
        int prefixes;
        int id;
        Dictionary<Type, string> primitives;
        Stack<string> namespaces;
        Dictionary<object, string> references;

        public DataContractSerializer(Type type)
        {
            namespaces = new Stack<string>();
            references = new Dictionary<object, string>();
            id = 0;
            rootElementType = GetArrayType(type);
            primitives = new Dictionary<Type, string>();
            primitives[typeof(string)] = "string";
            primitives[typeof(int)] = "int";
            primitives[typeof(bool)] = "boolean";
            primitives[typeof(float)] = "float";
            primitives[typeof(object)] = "anyType";
        }

        public IEnumerable WriteObject(XmlWriter writer, object graph)
        {
            yield return graph;
            this.writer = writer;
            string type = GetTypeString(graph.GetType());
            foreach (var item in WriteField(type, graph.GetType(), graph, Namespace))
            {
                yield return item;
            }
        }

        Type GetArrayType(Type arrayType)
        {
            Type type;
            Type[] elements = arrayType.GetGenericArguments();
            type = (elements == null || elements.Length == 0) ? arrayType.GetElementType() : elements[0];
            return type;
        }

        bool IsEmpty(IEnumerable array)
        {
            bool empty = true;
            foreach (var entry in array)
            {
                empty = false;
                break;
            }
            return empty;
        }

        void WriteNull(string field, Type type, string ns)
        {
            writer.WriteStartElement(field, ns);
            if (type.Namespace != null && type.Namespace != "System")
            {
                ns += type.Namespace;
                writer.LookupPrefix(ns);
            }
            else if (IsArray(type) && !namespaces.Contains(CollectionsNamespace))
            {
                ns = CollectionsNamespace;
                depth++;
                int added = WritePrefix(null, type, ns);
                if (added == 1)
                {
                    namespaces.Pop();
                }
                depth--;
            }
            writer.WriteAttributeString(XsiPrefix, XsiNilLocalName, XmlSchema.InstanceNamespace, "true");
            writer.WriteEndElement();
        }

        void WriteNull(string field, string ns)
        {
            writer.WriteStartElement(field, ns);
            writer.WriteAttributeString(XsiPrefix, XsiNilLocalName, XmlSchema.InstanceNamespace, "true");
            writer.WriteEndElement();
        }

        int WritePrefix(string prefix, Type type, string ns)
        {
            if (prefix == null)
            {
                prefix = writer.LookupPrefix(ns);
            }
            if (prefix == null)
            {
                prefixes++;
                prefix = string.Format(CultureInfo.InvariantCulture, $"d{depth}p{prefixes}");
            }
            if (prefix != string.Empty && type.Namespace != null && !namespaces.Contains(ns))
            {
                try
                {
                    writer.WriteAttributeString(XmlnsPrefix, prefix, null, ns);
                    namespaces.Push(ns);
                    return 1;
                }
                catch (XmlException e)
                {
                    Debug.LogError(e);
                }
            }
            return 0;
        }

        int WriteTypeNamespace(Type type, string ns)
        {
            writer.WriteStartAttribute(XsiPrefix, XsiTypeLocalName, XmlSchema.InstanceNamespace);
            try
            {
                writer.WriteQualifiedName(GetTypeString(type), ns);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            writer.WriteEndAttribute();
            namespaces.Push(ns);
            return 1;
        }

        bool IsTopNamespace(string ns)
        {
            return namespaces.Count == 0 ? false : namespaces.Peek() == ns;
        }

        string GetNamespace(Type reference, Type instance, string ns)
        {
            if (reference != null)
            {
                if (reference.Namespace != null && reference.Namespace != "System")
                {
                    ns = Namespace + reference.Namespace;
                }
                else
                {
                    ns = Namespace;
                }
            }
            else if (instance.Namespace != null)
            {
                ns = Namespace + instance.Namespace;
            }
            else if (instance.Namespace == null)
            {
                ns = Namespace;
            }
            return ns;
        }

        IEnumerable WriteField(string name, Type type, object value, string ns)
        {
            yield return value;
            depth++;
            prefixes = 0;
            writer.WriteStartElement(name, ns);
            Type valueType = value.GetType();
            Type element = GetArrayType(valueType);
            int addedNS = 0;
            if (valueType.IsDefined(typeof(DataContractAttribute), true) || (element != null && element != rootElementType && element.IsDefined(typeof(DataContractAttribute), true)))
            {
                string incomingNS = ns;
                ns = GetNamespace(element, valueType, ns);

                if (element != null || incomingNS != ns || !IsTopNamespace(ns))
                {
                    if (element != null)
                    {
                        if (!IsTopNamespace(ns))
                        {
                            addedNS += WritePrefix(null, element, ns);
                        }
                    }
                    else
                    {
                        addedNS += WritePrefix(null, valueType, ns);
                    }
                    if (type == typeof(object))
                    {
                        addedNS += WriteTypeNamespace(valueType, ns);
                    }
                }

                if (value is IEnumerable)
                {
                    foreach (var item in WriteDataContractEnumerable(value as IEnumerable, ns))
                    {
                        yield return item;
                    }
                }
                else if (value is Enum)
                {
                    writer.WriteString(value.ToString());
                }
                else
                {
                    DataContractAttribute contract = valueType.GetCustomAttribute<DataContractAttribute>();
                    if (contract != null && contract.IsReference)
                    {
                        if (references.TryGetValue(value, out string referenceId))
                        {
                            writer.WriteAttributeString(SerPrefix, RefLocalName, SerializationNamespace, referenceId);
                        }
                    }
                    else
                    {
                        foreach (var item in WriteDataContractObjectContents(value))
                        {
                            yield return item;
                        }
                    }

                }
            }
            else if (!(value is string) && value is IEnumerable)
            {
                if (element != null && element.IsDefined(typeof(DataContractAttribute), true))
                {
                    addedNS += WritePrefix(XsiPrefix, valueType, XmlSchema.InstanceNamespace);
                    foreach (var item in WriteDataContractEnumerable(value as IEnumerable, ns))
                    {
                        yield return item;
                    }
                }
                else
                {
                    if (IsArray(type) && !IsDictionary(type))
                    {
                        if (element != null && (element.Namespace == "System" || IsArray(element)))
                        {
                            ns = CollectionsNamespace;
                        }
                        else if (element != null && element.Namespace != null)
                        {
                            ns = Namespace + element.Namespace;
                        }
                    }
                    else
                    {
                        ns = CollectionsNamespace;
                    }
                    if (value is IDictionary)
                    {
                        addedNS += WritePrefix(null, valueType, ns);
                        foreach (var item in WriteDictionary(value as IDictionary, ns))
                        {
                            yield return item;
                        }
                    }
                    else
                    {
                        if (IsEmpty(value as IEnumerable))
                        {
                            if (element != null && element.Namespace != null)
                            {
                                addedNS += WritePrefix(null, valueType, ns);
                            }
                        }
                        else
                        {
                            if (!namespaces.Contains(ns))
                            {
                                addedNS += WritePrefix(null, valueType, ns);
                                if (type.Namespace == "System" && type != valueType)
                                {
                                    addedNS += WriteTypeNamespace(valueType, ns);
                                }
                            }
                            else if (!IsTopNamespace(ns))
                            {
                                writer.LookupPrefix(ns);
                            }
                            if (addedNS == 0)
                            {
                                namespaces.Push(ns);
                                addedNS++;
                            }
                            foreach (var item in WritePrimitiveEnumerable(value as IEnumerable, ns))
                            {
                                yield return item;
                            }
                        }
                    }
                }
            }
            else
            {
                if (type == typeof(object) && valueType.Namespace == "System")
                {
                    addedNS += WritePrefix(null, valueType, XmlSchema.Namespace);
                    addedNS += WriteTypeNamespace(valueType, XmlSchema.Namespace);
                }
                if (value is bool)
                {
                    writer.WriteString(XmlConvert.ToString((bool)value));
                }
                else if (value is double)
                {
                    writer.WriteValue((double)value);
                }
                else if (value is float)
                {
                    writer.WriteValue((float)value);
                }
                else if (value is int)
                {
                    writer.WriteValue((int)value);
                }
                else if (value is byte)
                {
                    writer.WriteValue((byte)value);
                }
                else if (value is string)
                {
                    writer.WriteValue((string)value);
                }
                else if (value is Enum)
                {
                    if (!namespaces.Contains(ns))
                    {
                        addedNS += WritePrefix(null, valueType, ns);
                        addedNS += WriteTypeNamespace(valueType, ns);
                    }
                    writer.WriteString(value.ToString());
                }
                else
                {
                    if (valueType.Namespace != null && !ns.EndsWith(valueType.Namespace))
                    {
                        ns += valueType.Namespace;
                        addedNS += WritePrefix(null, valueType, ns);
                        if (type.Namespace != valueType.Namespace)
                        {
                            addedNS += WriteTypeNamespace(valueType, ns);
                        }
                    }
                    else if (type == typeof(object))
                    {
                        ns = Namespace;
                        addedNS += WritePrefix(null, valueType, ns);
                        addedNS += WriteTypeNamespace(valueType, ns);
                    }
                    BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                    if (type.IsGenericType)
                    {
                        flags |= BindingFlags.NonPublic;
                    }
                    var members = GetSortedMembers(value, null, flags);
                    foreach (var entry in members)
                    {
                        if (entry.value != null)
                        {
                            foreach (var item in WriteField(entry.name, entry.type, entry.value, entry.ns))
                            {
                                yield return item;
                            }
                        }
                        else
                        {
                            WriteNull(entry.name, entry.ns);
                        }
                    }
                }
            }
            for (int i = 0; i < addedNS; ++i)
            {
                namespaces.Pop();
            }
            depth--;
            writer.WriteEndElement();
        }

        bool IsDictionary(Type type)
        {
            return type.GetInterface("IDictionary", false) != null;
        }

        bool IsArray(Type type)
        {
            bool isArray = type != typeof(string) && type.GetInterface("IEnumerable", false) != null;
            return isArray;
        }

        string GetTypeString(DictionaryEntry entry)
        {
            var builder = new StringBuilder();
            builder.Append("KeyValueOf");
            builder.Append(GetTypeString(entry.Key.GetType()));
            builder.Append(GetTypeString(entry.Value.GetType()));
            return builder.ToString();
        }

        string GetTypeString(Type type)
        {
            var stringBuilder = new StringBuilder();
            if (IsDictionary(type))
            {
                Type[] elements = type.GetGenericArguments();
                if (elements != null && elements.Length == 2)
                {
                    stringBuilder.Append("KeyValueOf");
                    stringBuilder.Append(GetTypeString(elements[0]));
                    stringBuilder.Append(GetTypeString(elements[1]));
                }
            }
            else if (IsArray(type))
            {
                Type element = GetArrayType(type);
                if (element != null)
                {
                    stringBuilder.Append("ArrayOf");
                    stringBuilder.Append(GetTypeString(element));
                }
            }
            else if (type.IsGenericType)
            {
                Type[] elements = type.GetGenericArguments();
                if (elements != null && elements.Length == 2)
                {
                    stringBuilder.Append("KeyValuePairOf");
                    stringBuilder.Append(GetTypeString(elements[0]));
                    stringBuilder.Append(GetTypeString(elements[1]));
                }
            }
            else
            {
                if (primitives.TryGetValue(type, out string primitive))
                {
                    stringBuilder.Append(primitive);
                }
                else if (type.Namespace != null && type.FullName.Contains(type.Namespace))
                {
                    stringBuilder.Append(type.FullName.Replace(type.Namespace + ".", "").Replace("+", "."));
                }
                else
                {
                    stringBuilder.Append(type.FullName.Replace("+", "."));
                }
            }
            return stringBuilder.ToString();
        }

        private IEnumerable WriteDataContractEnumerable(IEnumerable array, string ns)
        {
            depth++;
            prefixes = 0;
            string type = GetTypeString(GetArrayType(array.GetType()));
            foreach (var entry in array)
            {
                if (entry == null)
                {
                    yield return entry;
                    WriteNull(type, ns);
                }
                else
                {
                    writer.WriteStartElement(type, ns);
                    Type itemType = entry.GetType();
                    DataContractAttribute contract = itemType.GetCustomAttribute<DataContractAttribute>();
                    if (contract != null && contract.IsReference)
                    {
                        id++;
                        string referenceId = $"{XsiPrefix}{id}";
                        references.Add(entry, referenceId);
                        writer.WriteAttributeString(SerPrefix, IdLocalName, SerializationNamespace, referenceId);
                        string prefixNS = ns;
                        if (itemType.Namespace == null)
                        {
                            prefixNS = Namespace;
                        }
                        writer.LookupPrefix(prefixNS);
                        writer.WriteStartAttribute(XsiPrefix, XsiTypeLocalName, XmlSchema.InstanceNamespace);
                        writer.WriteQualifiedName(GetTypeString(itemType), prefixNS);
                        writer.WriteEndAttribute();
                    }
                    foreach (var item in WriteDataContractObjectContents(entry))
                    {
                        yield return item;
                    }
                    writer.WriteEndElement();
                }
            }
            depth--;
        }

        private IEnumerable WriteDictionary(IDictionary dictionary, string ns)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return entry;
                writer.WriteStartElement(null, GetTypeString(entry), ns);
                foreach (var item in WriteField("Key", entry.Key.GetType(), entry.Key, ns))
                {
                    yield return item;
                }
                foreach (var item in WriteField("Value", entry.Value.GetType(), entry.Value, ns))
                {
                    yield return item;
                }
                writer.WriteEndElement();
            }
        }

        private IEnumerable WritePrimitiveEnumerable(IEnumerable array, string ns)
        {
            Type type = GetArrayType(array.GetType());
            string description = GetTypeString(type);
            foreach (var entry in array)
            {
                if (entry == null)
                {
                    yield return entry;
                    WriteNull(description, ns);
                }
                else
                {
                    foreach (var item in WriteField(description, type, entry, ns))
                    {
                        yield return item;
                    }
                }
            }
        }

        class MemberEntry
        {
            public string name;
            public Type type;
            public object value;
            public string ns;

            public MemberEntry(string name, Type type, object value, string ns)
            {
                this.name = name;
                this.type = type;
                this.value = value;
                this.ns = ns;
            }
        }

        void AddSortedMembers(List<MemberEntry> list, object graph, Type type, Type filter, BindingFlags flags)
        {
            string ns = GetNamespace(type, graph.GetType(), Namespace);
            var sortedList = new SortedList<string, (Type, object)>();
            if (type.BaseType != null)
            {
                AddSortedMembers(list, graph, type.BaseType, filter, flags);
            }
            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetSetMethod(true) == null)
                {
                    continue;
                }
                MethodInfo method = property.GetGetMethod();
                if (method != null && method.GetParameters().Length > 0)
                {
                    continue;
                }
                if (filter == null || property.IsDefined(filter, true))
                {
                    if (property.GetIndexParameters().Length == 0)
                    {
                        object value = property.GetValue(graph);
                        sortedList.Add(property.Name, (property.PropertyType, value));
                    }
                }
            }
            foreach (var (name, (memberType, value)) in sortedList)
            {
                if (!list.Exists((entry) => entry.name == name))
                {
                    list.Add(new MemberEntry(name, memberType, value, ns));
                }
            }
            sortedList.Clear();
            foreach (var field in type.GetFields(flags))
            {
                if (filter == null || field.IsDefined(filter, true))
                {
                    object value = field.GetValue(graph);
                    sortedList.Add(field.Name, (field.FieldType, value));
                }
            }
            foreach (var (name, (memberType, value)) in sortedList)
            {
                if (!list.Exists((entry) => entry.name == name))
                {
                    list.Add(new MemberEntry(name, memberType, value, ns));
                }
            }
        }

        List<MemberEntry> GetSortedMembers(object graph, Type filter, BindingFlags flags)
        {
            Type type = graph.GetType();
            var members = new List<MemberEntry>();
            AddSortedMembers(members, graph, type, filter, flags);
            return members;
        }

        private IEnumerable WriteDataContractObjectContents(object graph)
        {
            var members = GetSortedMembers(graph, typeof(DataMemberAttribute), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var entry in members)
            {
                if (entry.value != null)
                {
                    foreach (var item in WriteField(entry.name, entry.type, entry.value, entry.ns))
                    {
                        yield return item;
                    }
                }
                else
                {
                    yield return entry.value;
                    WriteNull(entry.name, entry.type, entry.ns);
                }
            }
        }
    }
}
