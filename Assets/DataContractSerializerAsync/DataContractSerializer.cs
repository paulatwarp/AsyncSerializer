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
        public const string XsiTypeLocalName = "type";
        public const string XsiPrefix = "i";
        public const string XmlnsPrefix = "xmlns";
        public const string XsiNilLocalName = "nil";
        Type rootElementType;
        XmlWriter writer;
        int depth;
        int prefixes;
        Dictionary<Type, string> primitives;
        Stack<string> namespaces;

        public DataContractSerializer(Type type)
        {
            namespaces = new Stack<string>();
            rootElementType = GetArrayType(type);
            primitives = new Dictionary<Type, string>();
            primitives[typeof(string)] = "string";
            primitives[typeof(int)] = "int";
        }

        public IEnumerable WriteObject(XmlWriter writer, object graph)
        {
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

        void WriteNull(string field)
        {
            writer.WriteStartElement(field, Namespace);
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

        IEnumerable WriteField(string name, Type type, object value, string ns)
        {
            depth++;
            prefixes = 0;
            writer.WriteStartElement(name, ns);
            Type valueType = value.GetType();
            Type element = GetArrayType(valueType);
            bool namespaced = false;
            if (valueType.IsDefined(typeof(DataContractAttribute), true) || (element != null && element != rootElementType && element.IsDefined(typeof(DataContractAttribute), true)))
            {
                if (!namespaces.Contains(ns))
                {
                    if (value is IEnumerable)
                    {
                        writer.LookupPrefix(ns);
                    }
                    else
                    {
                        WritePrefix(null, valueType, ns);
                    }
                    namespaced = WriteTypeNamespace(valueType, ns);
                }
                if (value is IEnumerable)
                {
                    foreach (var item in WriteDataContractEnumerable(value as IEnumerable, ns))
                    {
                        yield return item;
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
                    if (element == null || element.Namespace == "System")
                    {
                        ns = CollectionsNamespace;
                    }
                    else
                    {
                        ns += element.Namespace;
                    }
                    WritePrefix(null, valueType, ns);
                    if (!IsEmpty(value as IEnumerable))
                    {
                        if (type.Namespace == "System")
                        {
                            namespaced = WriteTypeNamespace(valueType, ns);
                        }
                        foreach (var item in WritePrimitiveEnumerable(value as IEnumerable, ns))
                        {
                            yield return item;
                        }
                    }
                }
            }
            else
            {
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
                else if (valueType.Namespace == "UnityEngine")
                {
                    if (!ns.EndsWith("UnityEngine"))
                    {
                        ns += valueType.Namespace;
                        WritePrefix(null, valueType, ns);
                        if (type.Namespace != "UnityEngine")
                        {
                            namespaced = WriteTypeNamespace(valueType, ns);
                        }
                    }
                    foreach (var member in valueType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        foreach (var item in WriteField(member.Name, member.FieldType, member.GetValue(value), ns))
                        {
                            yield return item;
                        }
                    }
                }
                else
                {
                    if (type == typeof(object))
                    {
                        WritePrefix(null, valueType, XmlSchema.Namespace);
                        namespaced = WriteTypeNamespace(valueType, XmlSchema.Namespace);
                    }
                    writer.WriteString(value.ToString());
                }
            }
            if (namespaced)
            {
                namespaces.Pop();
            }
            depth--;
            writer.WriteEndElement();
        }

        string GetTypeString(Type type)
        {
            Type element = GetArrayType(type);
            var stringBuilder = new StringBuilder();
            if (element != null)
            {
                stringBuilder.Append("ArrayOf");
                stringBuilder.Append(GetTypeString(element));
            }
            else
            {
                if (primitives.TryGetValue(type, out string primitive))
                {
                    stringBuilder.Append(primitive);
                }
                else if (type.Namespace != null && type.FullName.Contains(type.Namespace))
                {
                    stringBuilder.Append(type.Name);
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
            foreach (var item in array)
            {
                if (item == null)
                {
                    WriteNull(GetTypeString(GetArrayType(array.GetType())));
                }
                else
                {
                    Type type = item.GetType();
                    string description = GetTypeString(type);
                    writer.WriteStartElement(description, Namespace);
                    foreach (var subitem in WriteDataContractObjectContents(item, ns))
                    {
                        yield return subitem;
                    }
                    writer.WriteEndElement();
                }
            }
            depth--;
        }

        private IEnumerable WritePrimitiveEnumerable(IEnumerable array, string ns)
        {
            foreach (var item in array)
            {
                Type type = item.GetType();
                string description = GetTypeString(type);
                foreach (var sequencePoint in WriteField(description, type, item, ns))
                {
                    yield return sequencePoint;
                }
            }
        }

        private IEnumerable WriteDataContractObjectContents(object graph, string ns)
        {
            Type type = graph.GetType();
            var members = new SortedList<string, (Type, object)>();
            foreach (var property in type.GetProperties())
            {
                if (property.IsDefined(typeof(DataMemberAttribute), true))
                {
                    if (property.GetIndexParameters().Length == 0)
                    {
                        object value = property.GetValue(graph);
                        members.Add(property.Name, (property.PropertyType, value));
                    }
                }
            }
            foreach (var field in type.GetFields())
            {
                if (field.IsDefined(typeof(DataMemberAttribute), true))
                {
                    object value = field.GetValue(graph);
                    members.Add(field.Name, (field.FieldType, value));
                }
            }
            foreach (var (name, (fieldType, value)) in members)
            {
                if (value != null)
                {
                    foreach (var item in WriteField(name, fieldType, value, ns))
                    {
                        yield return item;
                    }
                }
                else
                {
                    WriteNull(name);
                }
            }
        }
    }
}
