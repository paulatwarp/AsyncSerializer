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
            string typeName = GetTypeString(graph.GetType());
            namespaces.Push(Namespace);
            foreach (var item in WriteField(typeName, typeof(object), graph.GetType(), graph))
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
                string prefix = LookupPrefix(null, type, ns);
                writer.WriteAttributeString(XmlnsPrefix, prefix, null, ns);
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

        string LookupPrefix(string prefix, Type type, string ns)
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
            return prefix;
        }

        string GetNamespace(Type type, string ns)
        {
            if (type.Namespace == null)
            {
                ns = Namespace;
            }
            else
            {
                if (type.Namespace != "System")
                {
                    ns = Namespace + type.Namespace;
                }
            }
            return ns;
        }

        string GetNamespace(Type field, Type instance, string ns)
        {
            if (field != null && field != instance)
            {
                ns = GetNamespace(field, ns);
            }
            else
            {
                ns = GetNamespace(instance, ns);
            }
            return ns;
        }

        void WriteNamespace(Type fieldType, Type valueType, string ns)
        {
            if (fieldType != valueType)
            {
                string prefix = LookupPrefix(null, valueType, ns);
                if (prefix != string.Empty)
                {
                    writer.WriteAttributeString(XmlnsPrefix, prefix, null, ns);
                }
            }
        }

        void WriteType(Type fieldType, Type valueType, string ns)
        {
            if (fieldType != valueType)
            {
                try
                {
                    writer.WriteStartAttribute(XsiPrefix, XsiTypeLocalName, XmlSchema.InstanceNamespace);
                    writer.WriteQualifiedName(GetTypeString(valueType), ns);
                    writer.WriteEndAttribute();
                }
                catch (Exception exception)
                {
                    Debug.LogError(exception);
                }
            }
        }

        IEnumerable WriteField(string fieldName, Type fieldType, Type valueType, object value)
        {
            yield return value;
            depth++;
            string ns = namespaces.Peek();
            prefixes = 0;
            writer.WriteStartElement(fieldName, ns);
            
            if (primitives.ContainsKey(valueType))
            {
                ns = XmlSchema.Namespace;
                WriteNamespace(fieldType, valueType, ns);
                WriteType(fieldType, valueType, ns);
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
                WriteNamespace(fieldType, valueType, ns);
                WriteType(fieldType, valueType, ns);
                writer.WriteString(value.ToString());
            }
            else if (value is IDictionary)
            {
                ns = CollectionsNamespace;

                WriteNamespace(null, valueType, ns);

                namespaces.Push(ns);
                foreach (var item in WriteDictionary(value as IDictionary))
                {
                    yield return item;
                }
                namespaces.Pop();
            }
            else if (value is IEnumerable)
            {
                Type element = GetArrayType(valueType);
                if (depth == 1)
                {
                    string prefix = LookupPrefix(XsiPrefix, valueType, XmlSchema.InstanceNamespace);
                    writer.WriteAttributeString(XmlnsPrefix, prefix, null, XmlSchema.InstanceNamespace);
                }
                else
                {
                    if (IsArray(element))
                    {
                        ns = CollectionsNamespace;
                    }
                    else
                    {
                        ns = GetNamespace(element, valueType, CollectionsNamespace);
                    }

                    if (element == typeof(object) || element.Namespace != null)
                    {
                        if (fieldType != valueType)
                        {
                            WriteNamespace(fieldType, valueType, ns);
                            WriteType(fieldType, valueType, ns);
                        }
                        else
                        {
                            string prefix = LookupPrefix(null, valueType, ns);
                            if (prefix != string.Empty)
                            {
                                writer.WriteAttributeString(XmlnsPrefix, prefix, null, ns);
                            }
                        }
                    }
                    else
                    {
                        WriteNamespace(fieldType, valueType, ns);
                        WriteType(fieldType, valueType, ns);
                    }
                }

                foreach (var item in WriteEnumerable(value as IEnumerable))
                {
                    yield return item;
                }
            }
            else
            {
                Type filter = null;
                DataContractAttribute contract = valueType.GetCustomAttribute<DataContractAttribute>();
                if (contract != null)
                {
                    filter = typeof(DataMemberAttribute);
                }
                if (contract != null && contract.IsReference)
                {
                    if (references.TryGetValue(value, out string referenceId))
                    {
                        if (fieldType == typeof(object))
                        {
                            ns = GetNamespace(null, valueType, ns);
                            WriteNamespace(fieldType, valueType, ns);
                            WriteType(fieldType, valueType, ns);
                        }
                        writer.WriteAttributeString(SerPrefix, RefLocalName, SerializationNamespace, referenceId);
                    }
                    else
                    {
                        id++;
                        referenceId = $"{XsiPrefix}{id}";
                        references.Add(value, referenceId);
                        writer.WriteAttributeString(SerPrefix, IdLocalName, SerializationNamespace, referenceId);
                        string prefixNS = ns;
                        if (fieldType != valueType)
                        {
                            prefixNS = GetNamespace(null, valueType, ns);
                        }
                        writer.LookupPrefix(prefixNS);
                        WriteType(null, valueType, prefixNS);
                        namespaces.Push(ns);
                        foreach (var item in WriteObjectContent(value, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, filter))
                        {
                            yield return item;
                        }
                        namespaces.Pop();
                    }
                }
                else
                {
                    string prefixNS = GetNamespace(null, valueType, ns);
                    if (fieldType == typeof(object) || prefixNS != ns)
                    {
                        string prefix = LookupPrefix(null, valueType, prefixNS);
                        if (prefix != string.Empty)
                        {
                            writer.WriteAttributeString(XmlnsPrefix, prefix, null, prefixNS);
                            if (fieldType == typeof(object))
                            {
                                WriteType(null, valueType, prefixNS);
                            }
                        }
                        else
                        {
                            WriteType(null, valueType, prefixNS);
                        }
                        ns = prefixNS;
                    }
                    namespaces.Push(ns);
                    BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                    if (contract != null || fieldType.IsGenericType)
                    {
                        flags |= BindingFlags.NonPublic;
                    }
                    foreach (var item in WriteObjectContent(value, flags, filter))
                    {
                        yield return item;
                    }
                    namespaces.Pop();
                }

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

        private IEnumerable WriteEnumerable(IEnumerable array)
        {
            prefixes = 0;
            Type arrayType = array.GetType();
            Type entryType = GetArrayType(array.GetType());
            string typeName = GetTypeString(entryType);
            foreach (var value in array)
            {
                string ns = CollectionsNamespace;
                if (value == null)
                {
                    yield return value;
                    ns = GetNamespace(entryType, ns);
                    WriteNull(typeName, ns);
                }
                else
                {
                    Type valueType = value.GetType();
                    if (arrayType != typeof(object[]))
                    {
                        if (!IsArray(entryType) && valueType != null && valueType != typeof(object) && valueType.Namespace != "System")
                        {
                            ns = GetNamespace(entryType, valueType, ns);
                        }
                    }
                    namespaces.Push(ns);
                    foreach (var item in WriteField(typeName, entryType, valueType, value))
                    {
                        yield return item;
                    }
                    namespaces.Pop();
                }
            }
        }

        private IEnumerable WriteDictionary(IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return entry;
                string ns = namespaces.Peek();
                writer.WriteStartElement(null, GetTypeString(entry), ns);
                namespaces.Push(CollectionsNamespace);
                foreach (var item in WriteField("Key", entry.Key.GetType(), entry.Key.GetType(), entry.Key))
                {
                    yield return item;
                }
                foreach (var item in WriteField("Value", entry.Value.GetType(), entry.Value.GetType(), entry.Value))
                {
                    yield return item;
                }
                namespaces.Pop();
                writer.WriteEndElement();
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

        private IEnumerable WriteObjectContent(object graph, BindingFlags flags, Type filter)
        {
            var members = GetSortedMembers(graph, filter, flags);
            foreach (var entry in members)
            {
                if (entry.value != null)
                {
                    Type valueType = entry.value.GetType();
                    namespaces.Push(entry.ns);
                    foreach (var item in WriteField(entry.name, entry.type, valueType, entry.value))
                    {
                        yield return item;
                    }
                    namespaces.Pop();
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
