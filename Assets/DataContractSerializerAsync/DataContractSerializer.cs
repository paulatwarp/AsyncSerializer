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
    using System.Linq;
    
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
            primitives[typeof(string)] = "string";
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
                if (value is Array || value is not IEnumerable || !IsEmpty(value as IEnumerable))
                {
                    string prefix = writer.LookupPrefix(Namespace);
                    writer.WriteStartAttribute(XsiPrefix, XsiTypeLocalName, XmlSchema.InstanceNamespace);
                    prefix = writer.LookupPrefix(Namespace);
                    writer.WriteString(prefix);
                    writer.WriteString(":");
                    writer.WriteString(GetTypeString(type));
                    writer.WriteEndAttribute();
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
                    foreach (var item in WritePrimitiveEnumerable(value as IEnumerable))
                    {
                        yield return item;
                    }
                }
                else
                {
                    foreach (var item in WriteDataContractEnumerable(value as IEnumerable))
                    {
                        yield return item;
                    }
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
            var members = new SortedList<string, object>();
            foreach (var property in type.GetProperties())
            {
                if (property.IsDefined(typeof(DataMemberAttribute), true))
                {
                    if (property.GetIndexParameters().Length == 0)
                    {
                        object value = property.GetValue(graph);
                        if (value != null)
                        {
                            members.Add(property.Name, value);
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
                        members.Add(field.Name, value);
                    }
                }
            }
            foreach (var (name, value) in members)
            {
                foreach (var item in WriteField(name, value))
                {
                    yield return item;
                }
            }
        }
    }
}
