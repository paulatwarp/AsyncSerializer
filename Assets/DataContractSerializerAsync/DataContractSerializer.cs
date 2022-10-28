namespace AsyncSerialization
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml;
    using System.Xml.Schema;

    public class DataContractSerializer
    {
        public const string Namespace = "http://schemas.datacontract.org/2004/07/";
        public const string CollectionsNamespace = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
        public const string XsiTypeLocalName = "type";
        public const string XsiPrefix = "i";
        public const string XmlnsPrefix = "xmlns";
        Type rootType;
        Type rootElementType;
        XmlWriter writer;
        int depth;
        int prefixes;
        Dictionary<Type, string> primitives;

        public DataContractSerializer(Type type)
        {
            rootType = type;
            rootElementType = GetArrayType(type);
            primitives = new Dictionary<Type, string>();
            primitives[typeof(System.String)] = "string";
        }

        public IEnumerable WriteObject(XmlWriter writer, object graph)
        {
            this.writer = writer;
            string type = GetTypeString(graph.GetType());
            foreach (var item in WriteField(type, graph))
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

        IEnumerable WriteField(string field, object value)
        {
            writer.WriteStartElement(field, Namespace);
            Type type = value.GetType();
            Type element = GetArrayType(type);
            if (type == rootType)
            {
                writer.WriteAttributeString(XmlnsPrefix, XsiPrefix, null, XmlSchema.InstanceNamespace);
            }
            if (type.IsDefined(typeof(DataContractAttribute), true) || (element != null && element != rootElementType && element.IsDefined(typeof(DataContractAttribute), true)))
            {
                depth++;
                prefixes = 0;
                string prefix = writer.LookupPrefix(Namespace);
                writer.WriteStartAttribute(XsiPrefix, XsiTypeLocalName, XmlSchema.InstanceNamespace);
                prefix = writer.LookupPrefix(Namespace);
                writer.WriteString(prefix);
                writer.WriteString(":");
                writer.WriteString(GetTypeString(value.GetType()));
                writer.WriteEndAttribute();
                if (value is IEnumerable)
                {
                    foreach (var item in WriteEnumerable(value as IEnumerable))
                    {
                        yield return item;
                    }
                }
                else
                {
                    foreach (var item in WriteObjectContents(value))
                    {
                        yield return item;
                    }
                }
                depth--;
            }
            else if (!(value is string) && value is IEnumerable)
            {
                depth++;
                prefixes = 0;
                if (type != rootType && element != null && !element.IsDefined(typeof(DataContractAttribute), true))
                {
                    string prefix = writer.LookupPrefix(CollectionsNamespace);
                    if (prefix == null)
                    {
                        prefixes++;
                        prefix = string.Format(CultureInfo.InvariantCulture, $"d{depth}p{prefixes}");
                    }
                    writer.WriteAttributeString("xmlns", prefix, null, CollectionsNamespace);
                }
                foreach (var item in WriteEnumerable(value as IEnumerable))
                {
                    yield return item;
                }
                depth--;
            }
            else
            {
                writer.WriteString(value.ToString());
            }
            writer.WriteEndElement();
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
                if (primitives.TryGetValue(type, out string primitive))
                {
                    stringBuilder.Append(primitive);
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
            depth++;
            prefixes = 0;
            foreach (var item in array as IEnumerable)
            {
                Type itemType = item.GetType();
                string description = GetTypeString(itemType);
                if (itemType.IsDefined(typeof(DataContractAttribute), true))
                {
                    writer.WriteStartElement(description, Namespace);
                    foreach (var subitem in WriteObjectContents(item))
                    {
                        yield return subitem;
                    }
                }
                else
                {
                    writer.WriteStartElement(description, CollectionsNamespace);
                    writer.WriteString(item.ToString());
                }
                writer.WriteEndElement();
            }
            depth--;
        }


        private IEnumerable WriteObjectContents(object graph)
        {
            Type type = graph.GetType();
            yield return graph;
            if (type.IsDefined(typeof(DataContractAttribute), true))
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
                                foreach (var item in WriteField(property.Name, value))
                                {
                                    yield return item;
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
                            foreach (var item in WriteField(field.Name, value))
                            {
                                yield return item;
                            }
                        }
                    }
                }
            }
        }
    }
}
