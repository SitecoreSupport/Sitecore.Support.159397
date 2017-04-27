using Sitecore.Diagnostics;
using Sitecore.EmailCampaign.Analytics.Model;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Sitecore.Support
{
    [Serializable]
    class SerializableCustomValues
    {
        public SerializableCustomValues()
        {
            this.CustomValues = new SerializableDictionary();
        }

        public static implicit operator SerializableCustomValues(ExmCustomValues exmCustomValues)
        {
            if (exmCustomValues == null)
            {
                return null;
            }
            return new SerializableCustomValues
            {
                CustomValues = new SerializableDictionary(exmCustomValues.CustomValues),
                DispatchType = exmCustomValues.DispatchType,
                Email = exmCustomValues.Email,
                MessageLanguage = exmCustomValues.MessageLanguage,
                ManagerRootId = exmCustomValues.ManagerRootId,
                MessageId = exmCustomValues.MessageId,
                TestValueIndex = exmCustomValues.TestValueIndex
            };
        }

        public static implicit operator ExmCustomValues(SerializableCustomValues serializableExmCustomValues)
        {
            if (serializableExmCustomValues == null)
            {
                return null;
            }
            return new ExmCustomValues
            {
                CustomValues = serializableExmCustomValues.CustomValues,
                DispatchType = serializableExmCustomValues.DispatchType,
                Email = serializableExmCustomValues.Email,
                MessageLanguage = serializableExmCustomValues.MessageLanguage,
                ManagerRootId = serializableExmCustomValues.ManagerRootId,
                MessageId = serializableExmCustomValues.MessageId,
                TestValueIndex = serializableExmCustomValues.TestValueIndex
            };
        }

        public static bool ContainsCustomValuesKey(IDictionary<string, object> customData, out string key)
        {
            Assert.ArgumentNotNull(customData, "customData");
            key = null;
            if (customData.ContainsKey("ScExm"))
            {
                key = "ScExm";
            }
            else if (customData.ContainsKey("sc.ecm"))
            {
                key = "sc.ecm";
            }
            return (key != null);
        }

        // Properties
        public SerializableDictionary CustomValues { get; set; }

        public virtual DispatchType DispatchType { get; set; }

        public virtual string Email { get; set; }

        public virtual Guid ManagerRootId { get; set; }

        public virtual Guid MessageId { get; set; }

        public virtual string MessageLanguage { get; set; }

        public virtual byte? TestValueIndex { get; set; }
    }

    [XmlRoot("SerializableDictionary")]
    public class SerializableDictionary : Dictionary<String, Object>, IXmlSerializable
    {
        public SerializableDictionary()
        {
        }

        public SerializableDictionary(IDictionary<string, object> dictionary) : base(dictionary)
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Boolean wasEmpty = reader.IsEmptyElement;

            reader.Read();

            if (wasEmpty)
            {
                return;
            }

            while (reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.Name == "Item")
                {
                    String key = reader.GetAttribute("Key");
                    Type type = Type.GetType(reader.GetAttribute("TypeName"));

                    reader.Read();
                    if (type != null)
                    {
                        this.Add(key, new XmlSerializer(type).Deserialize(reader));
                    }
                    else
                    {
                        reader.Skip();
                    }
                    reader.ReadEndElement();

                    reader.MoveToContent();
                }
                else
                {
                    reader.ReadToFollowing("Item");
                }

                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (KeyValuePair<String, Object> item in this)
            {
                writer.WriteStartElement("Item");
                writer.WriteAttributeString("Key", item.Key);
                writer.WriteAttributeString("TypeName", item.Value.GetType().AssemblyQualifiedName);

                new XmlSerializer(item.Value.GetType()).Serialize(writer, item.Value);

                writer.WriteEndElement();
            }
        }
    }
}
