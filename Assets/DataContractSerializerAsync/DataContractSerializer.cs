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

        void WriteTypeNamespace(Type type, string ns)
        {
            writer.WriteStartAttribute(XsiPrefix, XsiTypeLocalName, XmlSchema.InstanceNamespace);
            writer.WriteQualifiedName(GetTypeString(type), ns);
            writer.WriteEndAttribute();
        }

        IEnumerable WriteField(string name, Type type, object value, string ns)
        {
            depth++;
            prefixes = 0;
            writer.WriteStartElement(name, ns);
            Type valueType = value.GetType();
            Type element = GetArrayType(valueType);
            if (valueType.IsDefined(typeof(DataContractAttribute), true) || (element != null && element != rootElementType && element.IsDefined(typeof(DataContractAttribute), true)))
            {
                bool namespaced = false;
                if (!namespaces.Contains(ns))
                {
                    WritePrefix(null, valueType, ns);
                    WriteTypeNamespace(valueType, ns);
                    namespaces.Push(ns);
                    namespaced = true;
                }
                if (value is IEnumerable)
                {
                    foreach (var item in WriteDataContractEnumerable(value as IEnumerable))
                    {
                        yield return item;
                    }
                }
                else
                {
                    foreach (var item in WriteDataContractObjectContents(value))
                    {
                        yield return item;
                    }
                }
                if (namespaced)
                {
                    namespaces.Pop();
                }
            }
            else if (!(value is string) && value is IEnumerable)
            {
                if (element != null && element.IsDefined(typeof(DataContractAttribute), true))
                {
                    WritePrefix(XsiPrefix, valueType, XmlSchema.InstanceNamespace);
                    foreach (var item in WriteDataContractEnumerable(value as IEnumerable))
                    {
                        yield return item;
                    }
                }
                else
                {
                    WritePrefix(null, valueType, CollectionsNamespace);
                    foreach (var item in WritePrimitiveEnumerable(value as IEnumerable))
                    {
                        yield return item;
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
                    WritePrefix(null, valueType, ns + valueType.Namespace);
                    WriteTypeNamespace(valueType, ns + valueType.Namespace);
                    namespaces.Push(ns + valueType.Namespace);
                    foreach (var member in valueType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        foreach (var item in WriteField(member.Name, member.FieldType, member.GetValue(value), ns + valueType.Namespace))
                        {
                            yield return item;
                        }
                    }
                    namespaces.Pop();
                }
                else
                {
                    if (type == typeof(object))
                    {
                        WritePrefix(null, valueType, XmlSchema.Namespace);
                        WriteTypeNamespace(valueType, XmlSchema.Namespace);
                    }
                    writer.WriteString(value.ToString());
                }
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

        private IEnumerable WriteDataContractEnumerable(IEnumerable array)
        {
            depth++;
            prefixes = 0;
            foreach (var item in array)
            {
                Type type = item.GetType();
                string description = GetTypeString(type);
                writer.WriteStartElement(description, Namespace);
                foreach (var subitem in WriteDataContractObjectContents(item))
                {
                    yield return subitem;
                }
                writer.WriteEndElement();
            }
            depth--;
        }

        private IEnumerable WritePrimitiveEnumerable(IEnumerable array)
        {
            foreach (var item in array)
            {
                Type type = item.GetType();
                string description = GetTypeString(type);
                writer.WriteStartElement(description, CollectionsNamespace);
                writer.WriteString(item.ToString());
                writer.WriteEndElement();
                yield return item;
            }
        }

        private IEnumerable WriteDataContractObjectContents(object graph)
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
                    foreach (var item in WriteField(name, fieldType, value, Namespace))
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
