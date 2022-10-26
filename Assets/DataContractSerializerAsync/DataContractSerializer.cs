namespace AsyncSerialization
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
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
        Type rootType;

        public DataContractSerializer(Type type)
        {
            rootType = type;
        }

        public IEnumerable WriteObject(XmlWriter writer, object graph)
        {
            InternalWriteStartObject(writer, graph);
            foreach (var item in InternalWriteObjectContent(writer, graph))
            {
                yield return item;
            }
            InternalWriteEndObject(writer);
        }

        string GetTypeString(Type type)
        {
            var stringBuilder = new StringBuilder();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type[] elements = type.GetGenericArguments();
                string element = GetTypeString(elements[0]);
                stringBuilder.Append("ArrayOf");
                stringBuilder.Append(element);
            }
            else
            {
                if (type.IsNested)
                {
                    stringBuilder.Append(type.DeclaringType);
                    stringBuilder.Append(".");
                }
                stringBuilder.Append(type.Name);
            }
            return stringBuilder.ToString();
        }

        private void InternalWriteStartObject(XmlWriter writer, object graph)
        {
            string type = GetTypeString(graph.GetType());
            writer.WriteStartElement(type, Namespace);
            if (graph.GetType() == rootType)
            {
                writer.WriteAttributeString(XmlnsPrefix, XsiPrefix, null, XmlSchema.InstanceNamespace);
            }
        }

        private IEnumerable InternalWriteObjectContent(XmlWriter writer, object graph)
        {
            Type type = graph.GetType();
            yield return graph;
            if (graph is IEnumerable)
            {
                foreach (var item in graph as IEnumerable)
                {
                    foreach (var subitem in WriteObject(writer, item))
                    {
                        yield return subitem;
                    }
                }
            }
            else if (type.IsDefined(typeof(DataContractAttribute), true))
            {
                foreach (var property in type.GetProperties())
                {
                    if (property.IsDefined(typeof(DataMemberAttribute), true))
                    {
                        if (property.GetIndexParameters().Length == 0)
                        {
                            object value = property.GetValue(graph);
                            if (value != null)
                            {
                                Type subType = value.GetType();
                                if (subType.IsDefined(typeof(DataContractAttribute), true))
                                {
                                    writer.WriteStartElement(property.Name);
                                    string prefix = writer.LookupPrefix(XmlSchema.InstanceNamespace);
                                    writer.WriteAttributeString(prefix, XsiTypeLocalName, XmlSchema.InstanceNamespace, GetTypeString(subType));
                                    foreach (var subItem in InternalWriteObjectContent(writer, value))
                                    {
                                        yield return subItem;
                                    }
                                    writer.WriteEndElement();
                                }
                                else
                                {
                                    writer.WriteStartElement(property.Name);
                                    writer.WriteString(value.ToString());
                                    writer.WriteEndElement();
                                }
                            }
                        }
                    }
                }
                foreach (var field in type.GetFields())
                {
                    if (field.IsDefined(typeof(DataMemberAttribute), true))
                    {
                        object value = field.GetValue(graph);
                        if (value != null)
                        {
                            if (value is IEnumerable)
                            {
                                writer.WriteStartElement(field.Name);
                                writer.WriteStartAttribute(null, XsiTypeLocalName, CollectionsNamespace);
                                writer.WriteEndAttribute();
                                Type[] elements = value.GetType().GetGenericArguments();
                                string element = GetTypeString(elements[0]);
                                foreach (var item in value as IEnumerable)
                                {
                                    writer.WriteStartElement(element, CollectionsNamespace);
                                    writer.WriteString(item.ToString());
                                    writer.WriteEndElement();
                                    yield return item;
                                }
                                writer.WriteEndElement();
                            }
                            else
                            {
                                writer.WriteStartElement(field.Name);
                                writer.WriteString(value.ToString());
                                writer.WriteEndElement();
                            }
                        }
                    }
                }
            }
        }

        private void InternalWriteEndObject(XmlWriter writer)
        {
            writer.WriteEndElement();
        }
    }
}
