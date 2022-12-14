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
        Dictionary<Type, List<MemberEntry>> typeInfo;

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

            typeInfo = new Dictionary<Type, List<MemberEntry>>();
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
            namespaces.Pop();
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
                WriteNamespace(null, type, ns);
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

        bool LookupPrefix(Type type, string ns, out string prefix)
        {
            bool generated = false;
            prefix = writer.LookupPrefix(ns);
            if (prefix == null)
            {
                prefixes++;
                prefix = string.Format(CultureInfo.InvariantCulture, $"d{depth}p{prefixes}");
                generated = true;
            }
            return generated;
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
                bool generated = LookupPrefix(valueType, ns, out string prefix);
                if (generated)
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
                    writer.WriteAttributeString(XmlnsPrefix, XsiPrefix, null, XmlSchema.InstanceNamespace);
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
                            if (fieldType == typeof(object))
                            {
                                WriteType(fieldType, valueType, ns);
                            }
                        }
                        else if (namespaces.Peek() != ns)
                        {
                            WriteNamespace(null, valueType, ns);
                        }
                    }
                    else
                    {
                        if (fieldType == typeof(object) || fieldType == valueType)
                        {
                            WriteNamespace(fieldType, valueType, ns);
                            WriteType(fieldType, valueType, ns);
                        }
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
                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                if (contract != null || fieldType.IsGenericType)
                {
                    flags |= BindingFlags.NonPublic;
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
                        namespaces.Push(ns);
                        if (fieldType != valueType)
                        {
                            ns = GetNamespace(null, valueType, ns);
                        }
                        WriteNamespace(fieldType, valueType, ns);
                        WriteType(fieldType, valueType, ns);
                        foreach (var item in WriteObjectContent(value, flags, filter))
                        {
                            yield return item;
                        }
                        namespaces.Pop();
                    }
                }
                else
                {
                    string prefixNS = GetNamespace(null, valueType, ns);
                    if (fieldType != valueType || prefixNS != ns)
                    {
                        ns = prefixNS;
                        WriteNamespace(null, valueType, ns);
                        WriteType(fieldType, valueType, ns);
                    }
                    namespaces.Push(ns);
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
            public PropertyInfo property;
            public FieldInfo field;
            public string ns;

            public MemberEntry(PropertyInfo property, string ns)
            {
                this.property = property;
                this.ns = ns;
            }

            public MemberEntry(FieldInfo field, string ns)
            {
                this.field = field;
                this.ns = ns;
            }

            public string GetName()
            {
                return (property != null)? property.Name : field.Name;
            }

            public object GetValue(object @object)
            {
                return (property != null)? property.GetValue(@object) : field.GetValue(@object);
            }

            new public Type GetType()
            {
                return (property != null) ? property.PropertyType : field.FieldType;
            }
        }

        void AddSortedMembers(List<MemberEntry> list, Type type, Type baseType, Type filter, BindingFlags flags)
        {
            string ns = GetNamespace(baseType, type, Namespace);
            var sortedList = new SortedList<string, MemberEntry>(StringComparer.Ordinal);
            if (baseType.BaseType != null)
            {
                if (!typeInfo.TryGetValue(baseType.BaseType, out List<MemberEntry> members))
                {
                    members = new List<MemberEntry>();
                    AddSortedMembers(members, type, baseType.BaseType, filter, flags);
                    typeInfo.Add(baseType.BaseType, members);
                }
                list.AddRange(members);
            }
            foreach (var property in baseType.GetProperties(flags))
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
                        sortedList.Add(property.Name, new MemberEntry(property, ns));
                    }
                }
            }
            foreach (var (name, member) in sortedList)
            {
                if (!list.Exists((entry) => entry.GetName() == name))
                {
                    list.Add(member);
                }
            }
            sortedList.Clear();
            foreach (var field in baseType.GetFields(flags))
            {
                if (filter == null || field.IsDefined(filter, true))
                {
                    sortedList.Add(field.Name, new MemberEntry(field, ns));
                }
            }
            foreach (var (name, member) in sortedList)
            {
                if (!list.Exists((entry) => entry.GetName() == name))
                {
                    list.Add(member);
                }
            }
        }

        List<MemberEntry> GetSortedMembers(Type type)
        {
            DataContractAttribute contract = type.GetCustomAttribute<DataContractAttribute>();
            Type filter = null;
            if (contract != null)
            {
                filter = typeof(DataMemberAttribute);
            }
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            if (contract != null || type.IsGenericType)
            {
                flags |= BindingFlags.NonPublic;
            }

            if (!typeInfo.TryGetValue(type, out List<MemberEntry> members))
            {
                members = new List<MemberEntry>();
                AddSortedMembers(members, type, type, filter, flags);
                typeInfo.Add(type, members);
            }
            return members;
        }

        public void CacheType(Type type)
        {
            GetSortedMembers(type);
        }

        private IEnumerable WriteObjectContent(object graph, BindingFlags flags, Type filter)
        {
            Type type = graph.GetType();
            var members = GetSortedMembers(type);
            foreach (var entry in members)
            {
                object value = entry.GetValue(graph);
                if (value != null)
                {
                    Type valueType = value.GetType();
                    namespaces.Push(entry.ns);
                    foreach (var item in WriteField(entry.GetName(), entry.GetType(), valueType, value))
                    {
                        yield return item;
                    }
                    namespaces.Pop();
                }
                else
                {
                    yield return value;
                    WriteNull(entry.GetName(), entry.GetType(), entry.ns);
                }
            }
        }
    }
}
