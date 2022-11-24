using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

public class XmlSpy : XmlWriter
{
    XmlWriter writer;
    List<string> log = new List<string>();
    List<string> compare;
    int position = 0;
    public bool verified = true;

    public XmlSpy(XmlWriter writer)
    {
        this.writer = writer;
    }

    public void WriteOriginalLog(string name)
    {
        using (var file = new StreamWriter(name))
        {
            foreach (var line in log)
            {
                file.WriteLine(line);
            }
            file.Flush();
        }
    }

    public void WriteComparisonLog(string name)
    {
        using (var file = new StreamWriter(name))
        {
            foreach (var line in compare)
            {
                file.WriteLine(line);
            }
            file.Flush();
        }
    }

    public void CheckLog(XmlWriter writer)
    {
        this.writer = writer;
        compare = new List<string>();
        verified = true;
        position = 0;
    }

    public override WriteState WriteState => writer.WriteState;

    public override void Flush()
    {
        LogLine("Flush");
        writer.Flush();
    }

    string PrintString(string s)
    {
        return s == null ? "null" : $"\"{s}\"";
    }

    void LogLine(string line)
    {
        line = line.Replace("\n", "\\n");
        if (compare != null)
        {
            if (verified)
            {
                if (position < log.Count)
                {
                    string expected = log[position];
                    if (expected != line)
                    {
                        verified = false;
                        Debug.LogError($"expected {expected} got {line} at line {position + 1}");
                    }
                }
                else
                {
                    verified = false;
                    Debug.LogError($"additional entry {line} at line {position + 1}");
                }
            }
            position++;
            compare.Add(line);
        }
        else
        {
            log.Add(line);
        }
    }

    public override string LookupPrefix(string ns)
    {
        string result = writer.LookupPrefix(ns);
        LogLine($"LookupPrefix({PrintString(ns)}) -> {PrintString(result)}");
        return result;
    }

    public override void WriteBase64(byte[] buffer, int index, int count)
    {
        LogLine($"WriteBase64({buffer}, {index}, {count})");
        writer.WriteBase64(buffer, index, count);
    }

    public override void WriteCData(string text)
    {
        LogLine($"WriteCData({PrintString(text)})");
        writer.WriteCData(text);
    }

    public override void WriteCharEntity(char ch)
    {
        LogLine($"WriteCharEntity('{(int)ch}')");
        writer.WriteCharEntity(ch);
    }

    public override void WriteChars(char[] buffer, int index, int count)
    {
        LogLine($"WriteChars({buffer}, {index}, {count})");
        writer.WriteChars(buffer, index, count);
    }

    public override void WriteComment(string text)
    {
        LogLine($"WriteComment({PrintString(text)}");
        writer.WriteComment(text);
    }

    public override void WriteQualifiedName(string localName, string ns)
    {
        writer.WriteQualifiedName(localName, ns);
    }

    public override void WriteDocType(string name, string pubid, string sysid, string subset)
    {
        LogLine($"WriteDocType({PrintString(name)}, {PrintString(pubid)}, {PrintString(sysid)}, {PrintString(subset)})");
        writer.WriteDocType(name, pubid, sysid, subset);
    }

    public override void WriteEndAttribute()
    {
        LogLine($"WriteEndAtrribute()");
        writer.WriteEndAttribute();
    }

    public override void WriteEndDocument()
    {
        LogLine($"WriteEndDocument()");
        writer.WriteEndDocument();
    }

    public override void WriteEndElement()
    {
        LogLine($"WriteEndElement()");
        writer.WriteEndElement();
    }

    public override void WriteEntityRef(string name)
    {
        LogLine($"WriteEntityRef({PrintString(name)})");
        writer.WriteEntityRef(name);
    }

    public override void WriteFullEndElement()
    {
        LogLine($"WriteFullEndElement()");
        writer.WriteFullEndElement();
    }

    public override void WriteProcessingInstruction(string name, string text)
    {
        LogLine($"WriteProcessingInstruction({PrintString(name)}, {PrintString(text)})");
        writer.WriteProcessingInstruction(name, text);
    }

    public override void WriteRaw(char[] buffer, int index, int count)
    {
        LogLine($"WriteRaw({buffer}, {index}, {count})");
        writer.WriteRaw(buffer, index, count);
    }

    public override void WriteRaw(string data)
    {
        LogLine($"WriteRaw({data})");
        writer.WriteRaw(data);
    }

    public override void WriteStartAttribute(string prefix, string localName, string ns)
    {
        LogLine($"WriteStartAttribute({PrintString(prefix)}, {PrintString(localName)}, {PrintString(ns)})");
        writer.WriteStartAttribute(prefix, localName, ns);
    }

    public override void WriteStartDocument()
    {
        LogLine($"WriteStartDocument()");
        writer.WriteStartDocument();
    }

    public override void WriteStartDocument(bool standalone)
    {
        LogLine($"WriteStartDocument({standalone})");
        writer.WriteStartDocument(standalone);
    }

    public override void WriteStartElement(string prefix, string localName, string ns)
    {
        LogLine($"WriteStartElement({PrintString(prefix)}, {PrintString(localName)}, {PrintString(ns)})");
        writer.WriteStartElement(prefix, localName, ns);
    }

    public override void WriteString(string text)
    {
        LogLine($"WriteString({PrintString(text)})");
        writer.WriteString(text);
    }

    public override void WriteSurrogateCharEntity(char lowChar, char highChar)
    {
        LogLine($"WriteSurrogateCharEntity({(int)lowChar}, {(int)highChar})");
        writer.WriteSurrogateCharEntity(lowChar, highChar);
    }

    public override void WriteWhitespace(string ws)
    {
        LogLine($"WriteWhitespace({PrintString(ws)})");
        writer.WriteWhitespace(ws);
    }
}
