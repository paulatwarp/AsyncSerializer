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
        const string Namespace = "http://schemas.datacontract.org/2004/07/";
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
            writer.WriteAttributeString("xmlns", "i", null, XmlSchema.InstanceNamespace);
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
            if (type.IsDefined(typeof(DataContractAttribute), true))
            {
                foreach (var property in type.GetProperties())
                {
                    if (property.IsDefined(typeof(DataMemberAttribute), true))
                    {
                        if (property.GetIndexParameters().Length == 0)
                        {
                            writer.WriteStartElement(property.Name);
                            object value = property.GetValue(graph);
                            if (value != null)
                            {
                                writer.WriteString(value.ToString());
                            }
                            writer.WriteEndElement();
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
