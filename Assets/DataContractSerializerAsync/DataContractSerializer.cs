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

        void WriteNull(string field, string ns)
        {
            writer.WriteStartElement(field, ns);
            writer.WriteAttributeString(XsiPrefix, XsiNilLocalName, XmlSchema.InstanceNamespace, "true");
            writer.WriteEndElement();
        }

        void WritePrefix(string prefix, Type type, string ns)
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
            if (type.Namespace != null)
            {
                writer.WriteAttributeString(XmlnsPrefix, prefix, null, ns);
            }
        }

        bool WriteTypeNamespace(Type type, string ns)
        {
            bool written = false;
            if (!namespaces.Contains(ns))
            {
                writer.WriteStartAttribute(XsiPrefix, XsiTypeLocalName, XmlSchema.InstanceNamespace);
                writer.WriteQualifiedName(GetTypeString(type), ns);
                writer.WriteEndAttribute();
                namespaces.Push(ns);
                written = true;
            }
            return written;
        }

        bool IsTopNamespace(string ns)
        {
            return namespaces.Count == 0 ? false : namespaces.Peek() == ns;
        }

        IEnumerable WriteField(string name, Type type, object value, string ns)
        {
            yield return value;
            depth++;
            prefixes = 0;
            writer.WriteStartElement(name, ns);
            Type valueType = value.GetType();
            Type element = GetArrayType(valueType);
            bool namespaced = false;
            if (valueType.IsDefined(typeof(DataContractAttribute), true) || (element != null && element != rootElementType && element.IsDefined(typeof(DataContractAttribute), true)))
            {
                if (element != null && element.Namespace != null)
                {
                    ns += element.Namespace;
                }
                if (!namespaces.Contains(ns))
                {
                    if (element != null)
                    {
                        WritePrefix(null, element, ns);
                    }
                    else
                    {
                        if (valueType.Namespace != null)
                        {
                            ns += valueType.Namespace;
                        }
                        WritePrefix(null, valueType, ns);
                    }
                    namespaced = WriteTypeNamespace(valueType, ns);
                }
                if (value is IEnumerable)
                {
                    if (!namespaced && type != valueType && ns != Namespace)
                    {
                        ns = Namespace;
                        WritePrefix(null, valueType, ns);
                        namespaced = WriteTypeNamespace(valueType, ns);
                    }
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
                    if (type == typeof(object) && !namespaces.Contains(Namespace))
                    {
                        ns = Namespace;
                        if (element == null && valueType.Namespace != null)
                        {
                            ns += valueType.Namespace;
                        }
                        else
                        {
                            WritePrefix(null, valueType, ns);
                            namespaced = WriteTypeNamespace(valueType, ns);
                        }
                    }
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
                        foreach (var item in WriteDataContractObjectContents(value, ns))
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
                    WritePrefix(XsiPrefix, valueType, XmlSchema.InstanceNamespace);
                    foreach (var item in WriteDataContractEnumerable(value as IEnumerable, ns))
                    {
                        yield return item;
                    }
                }
                else
                {
                    if (IsArray(type))
                    {
                        if (element != null && (element.Namespace == "System" || IsArray(element)))
                        {
                            ns = CollectionsNamespace;
                        }
                        else if (element != null && element.Namespace != null)
                        {
                            ns += element.Namespace;
                        }
                    }
                    else
                    {
                        ns = CollectionsNamespace;
                    }
                    if (value is IDictionary)
                    {
                        WritePrefix(null, valueType, ns);
                        foreach (var item in WriteDictionary(value as IDictionary, ns))
                        {
                            yield return item;
                        }
                    }
                    else
                    {
                        if (IsEmpty(value as IEnumerable))
                        {
                            if (element != null && element.Namespace != null && !namespaces.Contains(CollectionsNamespace))
                            {
                                WritePrefix(null, valueType, ns);
                            }
                        }
                        else
                        {
                            if (!namespaces.Contains(ns))
                            {
                                WritePrefix(null, valueType, ns);
                                if (type.Namespace == "System" && type != valueType)
                                {
                                    namespaced = WriteTypeNamespace(valueType, ns);
                                }
                            }
                            else if (!IsTopNamespace(ns))
                            {
                                writer.LookupPrefix(ns);
                            }
                            if (!namespaced)
                            {
                                namespaces.Push(ns);
                                namespaced = true;
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
                if (value is bool)
                {
                    if (type == typeof(object))
                    {
                        WritePrefix(null, valueType, XmlSchema.Namespace);
                        namespaced = WriteTypeNamespace(valueType, XmlSchema.Namespace);
                    }
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
                    if (type == typeof(object))
                    {
                        WritePrefix(null, valueType, XmlSchema.Namespace);
                        namespaced = WriteTypeNamespace(valueType, XmlSchema.Namespace);
                    }
                    writer.WriteValue((string)value);
                }
                else if (value is Enum)
                {
                    if (!namespaces.Contains(ns))
                    {
                        WritePrefix(null, valueType, ns);
                        namespaced = WriteTypeNamespace(valueType, ns);
                    }
                    writer.WriteString(value.ToString());
                }
                else
                {
                    if (valueType.Namespace != null && !ns.EndsWith(valueType.Namespace))
                    {
                        ns += valueType.Namespace;
                        WritePrefix(null, valueType, ns);
                        if (type.Namespace != "UnityEngine")
                        {
                            namespaced = WriteTypeNamespace(valueType, ns);
                        }
                    }
                    else if (type == typeof(object))
                    {
                        ns = Namespace;
                        WritePrefix(null, valueType, ns);
                        namespaced = WriteTypeNamespace(valueType, ns);
                    }
                    BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                    if (type.IsGenericType)
                    {
                        flags |= BindingFlags.NonPublic;
                    }
                    var members = GetSortedMembers(value, null, flags);
                    foreach (var (fieldName, fieldType, fieldValue) in members)
                    {
                        if (fieldValue != null)
                        {
                            foreach (var item in WriteField(fieldName, fieldType, fieldValue, ns))
                            {
                                yield return item;
                            }
                        }
                        else
                        {
                            WriteNull(fieldName, ns);
                        }
                    }
                }
            }
            if (namespaced)
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
            return type != typeof(string) && type.GetInterface("IEnumerable", false) != null;
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
                        writer.LookupPrefix(ns);
                        writer.WriteStartAttribute(XsiPrefix, XsiTypeLocalName, XmlSchema.InstanceNamespace);
                        writer.WriteQualifiedName(GetTypeString(itemType), ns);
                        writer.WriteEndAttribute();
                    }
                    foreach (var item in WriteDataContractObjectContents(entry, ns))
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

        void AddSorterMembers(List<(string, Type, object)> list, object graph, Type type, Type filter, BindingFlags flags)
        {
            var sortedList = new SortedList<string, (Type, object)>();
            if (type.BaseType != null)
            {
                AddSorterMembers(list, graph, type.BaseType, filter, flags);
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
                        if (!sortedList.ContainsKey(property.Name))
                        {
                            sortedList.Add(property.Name, (property.PropertyType, value));
                        }
                    }
                }
            }
            foreach (var (name, (memberType, value)) in sortedList)
            {
                if (!list.Contains((name, memberType, value)))
                {
                    list.Add((name, memberType, value));
                }
            }
            sortedList.Clear();
            foreach (var field in type.GetFields(flags))
            {
                if (filter == null || field.IsDefined(filter, true))
                {
                    object value = field.GetValue(graph);
                    if (!sortedList.ContainsKey(field.Name))
                    {
                        sortedList.Add(field.Name, (field.FieldType, value));
                    }
                }
            }
            foreach (var (name, (memberType, value)) in sortedList)
            {
                if (!list.Contains((name, memberType, value)))
                {
                    list.Add((name, memberType, value));
                }
            }
        }

        List<(string, Type, object)> GetSortedMembers(object graph, Type filter, BindingFlags flags)
        {
            Type type = graph.GetType();
            var members = new List<(string, Type, object)>();
            AddSorterMembers(members, graph, type, filter, flags);
            return members;
        }

        private IEnumerable WriteDataContractObjectContents(object graph, string ns)
        {
            var members = GetSortedMembers(graph, typeof(DataMemberAttribute), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var (name, type, value) in members)
            {
                if (value != null)
                {
                    foreach (var item in WriteField(name, type, value, ns))
                    {
                        yield return item;
                    }
                }
                else
                {
                    yield return value;
                    WriteNull(name, ns);
                }
            }
        }
    }
}
