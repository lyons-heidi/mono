//
// System.Xml.Serialization.XmlSerializationWriter.cs
//
// Author:
//   Tim Coleman (tim@timcoleman.com)
//   Lluis Sanchez Gual (lluis@ximian.com)
//
// Copyright (C) Tim Coleman, 2002
//

using System;
using System.Collections;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Runtime.Serialization;

namespace System.Xml.Serialization {
	public abstract class XmlSerializationWriter {

		#region Fields

		ObjectIDGenerator idGenerator;
		int qnameCount;
		bool topLevelElement = false;

		ArrayList namespaces;
		XmlWriter writer;
		Queue referencedElements;
		Hashtable callbacks;
		Hashtable serializedObjects;
		const string xmlNamespace = "http://www.w3.org/2000/xmlns/";

		#endregion // Fields

		#region Constructors

		protected XmlSerializationWriter ()
		{
			qnameCount = 0;
			serializedObjects = new Hashtable ();
		}
		
		internal void Initialize (XmlWriter writer, XmlSerializerNamespaces nss)
		{
			this.writer = writer;
			if (nss != null)
			{
				namespaces = new ArrayList ();
				foreach (XmlQualifiedName ns in nss.ToArray())
					if (ns.Name != "")
						namespaces.Add (ns);
			}	
		}

		#endregion // Constructors

		#region Properties

		protected ArrayList Namespaces {
			get { return namespaces; }
			set { namespaces = value; }
		}

		protected XmlWriter Writer {
			get { return writer; }
			set { writer = value; }
		}

		#endregion // Properties

		#region Methods
		
		protected void AddWriteCallback (Type type, string typeName, string typeNs, XmlSerializationWriteCallback callback)
		{
			WriteCallbackInfo info = new WriteCallbackInfo ();
			info.Type = type;
			info.TypeName = typeName;
			info.TypeNs = typeNs;
			info.Callback = callback;

			if (callbacks == null) callbacks = new Hashtable ();
			callbacks.Add (type, info);
		}

		protected Exception CreateMismatchChoiceException (string value, string elementName, string enumValue)
		{
			string message = String.Format ("Value of {0} mismatches the type of {1}, you need to set it to {2}.", elementName, value, enumValue);
			return new InvalidOperationException (message);
		}

		protected Exception CreateUnknownAnyElementException (string name, string ns)
		{
			string message = String.Format ("The XML element named '{0}' from namespace '{1}' was not expected. The XML element name and namespace must match those provided via XmlAnyElementAttribute(s).", name, ns);
			return new InvalidOperationException (message);
		}

		protected Exception CreateUnknownTypeException (object o)
		{
			return CreateUnknownTypeException (o.GetType ());
		}

		protected Exception CreateUnknownTypeException (Type type)
		{
			string message = String.Format ("The type {0} may not be used in this context.", type);
			return new InvalidOperationException (message);
		}

		protected static string FromByteArrayBase64 (byte[] value)
		{
			return XmlCustomFormatter.FromByteArrayBase64 (value);
		}

		protected static string FromByteArrayHex (byte[] value)
		{
			return XmlCustomFormatter.FromByteArrayHex (value);
		}

		protected static string FromChar (char value)
		{
			return XmlCustomFormatter.FromChar (value);
		}

		protected static string FromDate (DateTime value)
		{
			return XmlCustomFormatter.FromDate (value);
		}

		protected static string FromDateTime (DateTime value)
		{
			return XmlCustomFormatter.FromDateTime (value);
		}

		protected static string FromEnum (long value, string[] values, long[] ids)
		{
			return XmlCustomFormatter.FromEnum (value, values, ids);
		}

		protected static string FromTime (DateTime value)
		{
			return XmlCustomFormatter.FromTime (value);
		}

		protected static string FromXmlName (string name)
		{
			return XmlCustomFormatter.FromXmlName (name);
		}

		protected static string FromXmlNCName (string ncName)
		{
			return XmlCustomFormatter.FromXmlNCName (ncName);
		}

		protected static string FromXmlNmToken (string nmToken)
		{
			return XmlCustomFormatter.FromXmlNmToken (nmToken);
		}

		protected static string FromXmlNmTokens (string nmTokens)
		{
			return XmlCustomFormatter.FromXmlNmTokens (nmTokens);
		}

		protected string FromXmlQualifiedName (XmlQualifiedName xmlQualifiedName)
		{
			if (xmlQualifiedName == null || xmlQualifiedName == XmlQualifiedName.Empty)
				return null;
				
			return GetQualifiedName (xmlQualifiedName.Name, xmlQualifiedName.Namespace);
		}

		private string GetId (object o, bool addToReferencesList)
		{
			if (idGenerator == null) idGenerator = new ObjectIDGenerator ();

			bool firstTime;
			long lid = idGenerator.GetId (o, out firstTime);
			return String.Format ("id{0}", lid);
		}

		
		bool AlreadyQueued (object ob)
		{
			if (idGenerator == null) return false;

			bool firstTime;
			idGenerator.HasId (ob, out firstTime);
			return !firstTime;
		}

		private string GetNamespacePrefix (string ns)
		{
			string prefix = Writer.LookupPrefix (ns);
			if (prefix == null) 
			{
				prefix = String.Format ("q{0}", ++qnameCount);
				WriteAttribute ("xmlns", prefix, null, ns);
			}
			return prefix;
		}

		private string GetQualifiedName (string name, string ns)
		{
			if (ns == String.Empty) return name;
			
			string prefix = GetNamespacePrefix (ns);
			if (prefix == String.Empty)
				return name;
			else
				return String.Format ("{0}:{1}", prefix, name);
		}

		protected abstract void InitCallbacks ();

		protected void TopLevelElement ()
		{
			topLevelElement = true;
		}

		protected void WriteAttribute (string localName, byte[] value)
		{
			WriteAttribute (localName, String.Empty, value);
		}

		protected void WriteAttribute (string localName, string value)
		{
			WriteAttribute (String.Empty, localName, String.Empty, value);
		}

		protected void WriteAttribute (string localName, string ns, byte[] value)
		{
			if (value == null)
				return;

			Writer.WriteStartAttribute (localName, ns);
			WriteValue (value);
			Writer.WriteEndAttribute ();
		}

		protected void WriteAttribute (string localName, string ns, string value)
		{
			WriteAttribute (null, localName, ns, value);
		}

		protected void WriteAttribute (string prefix, string localName, string ns, string value)
		{
			if (value == null)
				return;

			Writer.WriteAttributeString (prefix, localName, ns, value);
		}

		protected void WriteElementEncoded (XmlNode node, string name, string ns, bool isNullable, bool any)
		{
			if (name != string.Empty)
			{
				if (node == null)
				{
					if (isNullable)
						WriteNullTagEncoded (name, ns);
				}
				else
				{
					Writer.WriteStartElement (name, ns);
					node.WriteTo (Writer);
					Writer.WriteEndElement ();
				}
			}
			else
				node.WriteTo (Writer);
		}

		protected void WriteElementLiteral (XmlNode node, string name, string ns, bool isNullable, bool any)
		{
			if (name != string.Empty)
			{
				if (node == null)
				{
					if (isNullable)
						WriteNullTagLiteral (name, ns);
				}
				else
				{
					Writer.WriteStartElement (name, ns);
					node.WriteTo (Writer);
					Writer.WriteEndElement ();
				}
			}
			else
				node.WriteTo (Writer);
		}

		protected void WriteElementQualifiedName (string localName, XmlQualifiedName value)
		{
			WriteElementQualifiedName (localName, String.Empty, value, null);
		}

		protected void WriteElementQualifiedName (string localName, string ns, XmlQualifiedName value)
		{
			WriteElementQualifiedName (localName, ns, value, null);
		}

		protected void WriteElementQualifiedName (string localName, XmlQualifiedName value, XmlQualifiedName xsiType)
		{
			WriteElementQualifiedName (localName, String.Empty, value, xsiType);
		}

		protected void WriteElementQualifiedName (string localName, string ns, XmlQualifiedName value, XmlQualifiedName xsiType)
		{
			localName = XmlCustomFormatter.FromXmlNCName (localName);
			WriteStartElement (localName, ns);
			if (xsiType != null) WriteXsiType (xsiType.Name, xsiType.Namespace);
			Writer.WriteString (FromXmlQualifiedName (value));
			WriteEndElement ();
		}

		protected void WriteElementString (string localName, string value)
		{
			WriteElementString (localName, String.Empty, value, null);
		}

		protected void WriteElementString (string localName, string ns, string value)
		{
			WriteElementString (localName, ns, value, null);
		}

		protected void WriteElementString (string localName, string value, XmlQualifiedName xsiType)
		{
			WriteElementString (localName, String.Empty, value, xsiType);
		}

		protected void WriteElementString (string localName, string ns, string value, XmlQualifiedName xsiType)
		{
			if (value == null) return;

			if (xsiType != null) {
				localName = XmlCustomFormatter.FromXmlNCName (localName);
				WriteStartElement (localName, ns);
				WriteXsiType (xsiType.Name, xsiType.Namespace);
				Writer.WriteString (value);
				WriteEndElement ();
			} 
			else
				Writer.WriteElementString (localName, ns, value);
		}

		protected void WriteElementStringRaw (string localName, byte[] value)
		{
			WriteElementStringRaw (localName, String.Empty, value, null);
		}

		protected void WriteElementStringRaw (string localName, string value)
		{
			WriteElementStringRaw (localName, String.Empty, value, null);
		}

		protected void WriteElementStringRaw (string localName, byte[] value, XmlQualifiedName xsiType)
		{
			WriteElementStringRaw (localName, String.Empty, value, xsiType);
		}

		protected void WriteElementStringRaw (string localName, string ns, byte[] value)
		{
			WriteElementStringRaw (localName, ns, value, null);
		}

		protected void WriteElementStringRaw (string localName, string ns, string value)
		{
			WriteElementStringRaw (localName, ns, value, null);
		}

		protected void WriteElementStringRaw (string localName, string value, XmlQualifiedName xsiType)
		{
			WriteElementStringRaw (localName, String.Empty, value, null);
		}

		[MonoTODO ("Implement")]
		protected void WriteElementStringRaw (string localName, string ns, byte[] value, XmlQualifiedName xsiType)
		{
			throw new NotImplementedException ();
		}

		protected void WriteElementStringRaw (string localName, string ns, string value, XmlQualifiedName xsiType)
		{
			localName = XmlCustomFormatter.FromXmlNCName (localName);
			WriteStartElement (localName, ns);

			if (xsiType != null)
				WriteXsiType (xsiType.Name, xsiType.Namespace);

			Writer.WriteRaw (value);
			WriteEndElement ();
		}

		protected void WriteEmptyTag (string name)
		{
			WriteEmptyTag (name, String.Empty);
		}

		protected void WriteEmptyTag (string name, string ns)
		{
			name = XmlCustomFormatter.FromXmlName (name);
			WriteStartElement (name, ns);
			WriteEndElement ();
		}

		protected void WriteEndElement ()
		{
			WriteEndElement (null);
		}

		protected void WriteEndElement (object o)
		{
			if (o != null)
				serializedObjects.Remove (o);
				
			Writer.WriteEndElement ();
		}

		protected void WriteId (object o)
		{
			WriteAttribute ("id", GetId (o, true));
		}

		protected void WriteNamespaceDeclarations (XmlSerializerNamespaces ns)
		{
			if (ns == null)
				return;

			ICollection namespaces = ns.Namespaces.Values;
			foreach (XmlQualifiedName qn in namespaces) {
				if (Writer.LookupPrefix (qn.Namespace) == null)
					WriteAttribute ("xmlns", qn.Name, xmlNamespace, qn.Namespace);
			}
		}

		protected void WriteNullableQualifiedNameEncoded (string name, string ns, XmlQualifiedName value, XmlQualifiedName xsiType)
		{
			if (value != null)
				WriteElementQualifiedName (name, ns, value, xsiType);
			else
				WriteNullTagEncoded (name, ns);
		}

		protected void WriteNullableQualifiedNameLiteral (string name, string ns, XmlQualifiedName value)
		{
			if (value != null)
				WriteElementQualifiedName (name, ns, value);
			else
				WriteNullTagLiteral (name, ns);
		}

		protected void WriteNullableStringEncoded (string name, string ns, string value, XmlQualifiedName xsiType)
		{
			if (value != null)
				WriteElementString (name, ns, value, xsiType);
			else
				WriteNullTagEncoded (name, ns);
		}

		[MonoTODO ("Implement")]
		protected void WriteNullableStringEncodedRaw (string name, string ns, byte[] value, XmlQualifiedName xsiType)
		{
			throw new NotImplementedException ();
		}

		[MonoTODO ("Implement")]
		protected void WriteNullableStringEncodedRaw (string name, string ns, string value, XmlQualifiedName xsiType)
		{
			throw new NotImplementedException ();
		}

		protected void WriteNullableStringLiteral (string name, string ns, string value)
		{
			if (value != null)
				WriteElementString (name, ns, value, null);
			else
				WriteNullTagLiteral (name, ns);
		}

		[MonoTODO ("Implement")]
		protected void WriteNullableStringLiteralRaw (string name, string ns, byte[] value)
		{
			throw new NotImplementedException ();
		}

		[MonoTODO ("Implement")]
		protected void WriteNullableStringLiteralRaw (string name, string ns, string value)
		{
			throw new NotImplementedException ();
		}

		protected void WriteNullTagEncoded (string name)
		{
			WriteNullTagEncoded (name, String.Empty);
		}

		protected void WriteNullTagEncoded (string name, string ns)
		{
			Writer.WriteStartElement (name, ns);
			Writer.WriteAttributeString ("null", XmlSchema.InstanceNamespace, "1");
			Writer.WriteEndElement ();
		}

		protected void WriteNullTagLiteral (string name)
		{
			WriteNullTagLiteral (name, String.Empty);
		}

		protected void WriteNullTagLiteral (string name, string ns)
		{
			Writer.WriteStartElement (name, ns);
			Writer.WriteAttributeString ("nil", XmlSchema.InstanceNamespace, "true");
			Writer.WriteEndElement ();
		}

		protected void WritePotentiallyReferencingElement (string n, string ns, object o)
		{
			WritePotentiallyReferencingElement (n, ns, o, null, false, false);
		}

		protected void WritePotentiallyReferencingElement (string n, string ns, object o, Type ambientType)
		{
			WritePotentiallyReferencingElement (n, ns, o, ambientType, false, false);
		}

		protected void WritePotentiallyReferencingElement (string n, string ns, object o, Type ambientType, bool suppressReference)
		{
			WritePotentiallyReferencingElement (n, ns, o, ambientType, suppressReference, false);
		}

		protected void WritePotentiallyReferencingElement (string n, string ns, object o, Type ambientType, bool suppressReference, bool isNullable)
		{
			if (o == null) 
			{
				if (isNullable) WriteNullTagEncoded (n, ns);
				return;
			}

			WriteStartElement (n, ns, true);

			CheckReferenceQueue ();

			if (callbacks.ContainsKey (o.GetType ()))
			{
				WriteCallbackInfo info = (WriteCallbackInfo) callbacks[o.GetType()];
				if (o.GetType ().IsEnum) {
					info.Callback (o);
				}
				else if (suppressReference) {
					Writer.WriteAttributeString ("id", GetId (o, false));
					if (ambientType != o.GetType ()) WriteXsiType(info.TypeName, info.TypeNs);
					info.Callback (o);
				}
				else {
					if (!AlreadyQueued (o)) referencedElements.Enqueue (o);
					Writer.WriteAttributeString ("href", "#" + GetId (o, true));
				}
			}
			else
			{
				// Must be a primitive type
				TypeData td = TypeTranslator.GetTypeData (o.GetType ());
				if (td.SchemaType != SchemaTypes.Primitive)
					throw new InvalidOperationException ("Invalid type: " + o.GetType().FullName);
				WriteXsiType(td.XmlType, XmlSchema.Namespace);
				Writer.WriteString (XmlCustomFormatter.ToXmlString (td, o));
			}

			WriteEndElement ();
		}

		protected void WriteReferencedElements ()
		{
			if (referencedElements == null) return;
			if (callbacks == null) return;

			while (referencedElements.Count > 0)
			{
				object o = referencedElements.Dequeue ();
				TypeData td = TypeTranslator.GetTypeData (o.GetType ());
				WriteCallbackInfo info = (WriteCallbackInfo) callbacks[o.GetType()];
				WriteStartElement (info.TypeName, info.TypeNs, true);
				Writer.WriteAttributeString ("id", GetId (o, false));

				if (td.SchemaType != SchemaTypes.Array)	// Array use its own "arrayType" attribute
					WriteXsiType(info.TypeName, info.TypeNs);

				info.Callback (o);
				WriteEndElement ();
			}
		}

		protected void WriteReferencingElement (string n, string ns, object o)
		{
			WriteReferencingElement (n, ns, o, false);
		}

		protected void WriteReferencingElement (string n, string ns, object o, bool isNullable)
		{
			if (o == null) 
			{
				if (isNullable) WriteNullTagEncoded (n, ns);
				return;
			}
			else
			{
				CheckReferenceQueue ();
				if (!AlreadyQueued (o)) referencedElements.Enqueue (o);

				Writer.WriteStartElement (n, ns);
				Writer.WriteAttributeString ("href", "#" + GetId (o, true));
				Writer.WriteEndElement ();
			}
		}

		void CheckReferenceQueue ()
		{
			if (referencedElements == null)  
			{
				referencedElements = new Queue ();
				InitCallbacks ();
			}
		}

		protected void WriteSerializable (IXmlSerializable serializable, string name, string ns, bool isNullable)
		{
			if (serializable == null)
			{
				if (isNullable) WriteNullTagLiteral (name, ns);
				return;
			}
			else
			{
				Writer.WriteStartElement (name, ns);
				serializable.WriteXml (Writer);
				Writer.WriteEndElement ();
			}
		}

		protected void WriteStartDocument ()
		{
			if (Writer.WriteState == WriteState.Start)
				Writer.WriteStartDocument ();
		}

		protected void WriteStartElement (string name)
		{
			WriteStartElement (name, String.Empty, null, false);
		}

		protected void WriteStartElement (string name, string ns)
		{
			WriteStartElement (name, ns, null, false);
		}

		protected void WriteStartElement (string name, string ns, bool writePrefixed)
		{
			WriteStartElement (name, ns, null, writePrefixed);
		}

		protected void WriteStartElement (string name, string ns, object o)
		{
			WriteStartElement (name, ns, o, false);
		}

		protected void WriteStartElement (string name, string ns, object o, bool writePrefixed)
		{
			if (o != null)
			{
				if (serializedObjects.Contains (o))
					throw new InvalidOperationException ("A cirtular reference was detected while serializing an object of type " + o.GetType().Name);
				else
					serializedObjects [o] = o;
			}
			
			WriteState oldState = Writer.WriteState;
			
			// Elements with schema namespace are always written prefixed
			if (ns == XmlSchema.Namespace) writePrefixed = true;

			string prefix = null;
			
			if (topLevelElement && ns != null && ns.Length != 0)
			{
				foreach (XmlQualifiedName qn in namespaces)
					if (qn.Namespace == ns) {
						prefix = qn.Name;
						writePrefixed = true;
						break;
					}
			}

			if (writePrefixed && ns != string.Empty)
			{
				name = XmlCustomFormatter.FromXmlName (name);
				
				if (prefix == null)
					prefix = Writer.LookupPrefix (ns);
				if (prefix == null || prefix.Length == 0) {
					if (ns == XmlSchema.Namespace) prefix = "xsd";
					else prefix = "q" + (++qnameCount);
				}
				Writer.WriteStartElement (prefix, name, ns);
			} else
				Writer.WriteStartElement (name, ns);

			if (topLevelElement) 
			{
				if (namespaces != null) {
					foreach (XmlQualifiedName qn in namespaces)
					{
						string currentPrefix = Writer.LookupPrefix (qn.Namespace);
						if (qn.Namespace == XmlSchema.Namespace || qn.Namespace == XmlSchema.InstanceNamespace) {
							if (currentPrefix == qn.Name) continue;
						}
						else 
							if (currentPrefix != null && currentPrefix.Length != 0) continue;
						
						WriteAttribute ("xmlns",qn.Name,xmlNamespace,qn.Namespace);
					}
				}
				topLevelElement = false;
			}
		}

		protected void WriteTypedPrimitive (string name, string ns, object o, bool xsiType)
		{
			string value;
			TypeData td = TypeTranslator.GetTypeData (o.GetType ());

			name = XmlCustomFormatter.FromXmlName (name);
			Writer.WriteStartElement (name, ns);

			if (o is XmlQualifiedName)
				value = FromXmlQualifiedName ((XmlQualifiedName) o);
			else
				value = XmlCustomFormatter.ToXmlString (td, o);

			if (xsiType)
			{
				if (td.SchemaType != SchemaTypes.Primitive)
					throw new InvalidOperationException ("Invalid type: " + o.GetType().FullName);
				WriteXsiType (td.XmlType, XmlSchema.Namespace);
			}

			WriteValue (value);
			Writer.WriteEndElement ();
		}

		protected void WriteValue (byte[] value)
		{
			Writer.WriteBase64 (value, 0, value.Length);
		}

		protected void WriteValue (string value)
		{
			if (value != null)
				Writer.WriteString (value);
		}

		protected void WriteXmlAttribute (XmlNode node)
		{
			WriteXmlAttribute (node, null);
		}

		protected void WriteXmlAttribute (XmlNode node, object container)
		{
			XmlAttribute attr = node as XmlAttribute;
			if (attr == null)
				throw new InvalidOperationException ("The node must be either type XmlAttribute or a derived type.");
			
			if (attr.NamespaceURI == XmlSerializer.WsdlNamespace)
			{
				// The wsdl arrayType attribute needs special handling
				if (attr.LocalName == "arrayType") {
					string ns, type, dimensions;
					TypeTranslator.ParseArrayType (attr.Value, out type, out ns, out dimensions);
					string value = GetQualifiedName (type + dimensions, ns);
					WriteAttribute (attr.Prefix, attr.LocalName, attr.NamespaceURI, value);
					return;
				}
			}
			
			WriteAttribute (attr.Prefix, attr.LocalName, attr.NamespaceURI, attr.Value);
		}

		protected void WriteXsiType (string name, string ns)
		{
			if (ns != null && ns != string.Empty)
				WriteAttribute ("type", XmlSchema.InstanceNamespace, GetQualifiedName (name, ns));
			else
				WriteAttribute ("type", XmlSchema.InstanceNamespace, name);
		}
		
		#endregion

		class WriteCallbackInfo
		{
			public Type Type;
			public string TypeName;
			public string TypeNs;
			public XmlSerializationWriteCallback Callback;
		}
	}
}
