# protobuf-net

Protocol Buffers library for Unity.

Preserving proto comments when generating C# with protobuf-net

### input proto file

    package pomelo.test;
    // This message is used to request a resource from the server
    message GetResource
    {
        // The identifier of the requested resource 
        required string resourceId = 1;
    }

### output cs file

    //------------------------------------------------------------------------------
    // <auto-generated>
    //     This code was generated by a tool.
    //
    //     Changes to this file may cause incorrect behavior and will be lost if
    //     the code is regenerated.
    // </auto-generated>
    //------------------------------------------------------------------------------
    // Generated from: test.proto
    namespace pomelo.test
    {
      /// <summary>
      /// This message is used to request a resource from the server
      /// </summary>
      [global::ProtoBuf.ServiceModel.ProtoComment("This message is used to request a resource from the server")]
      [global::System.Serializable, global::ProtoBuf.ProtoContract(Name=@"GetResource")]
      public partial class GetResource : global::ProtoBuf.IExtensible
      {
        public GetResource() {}
        
        private string _resourceId;
        /// <summary>
        /// The identifier of the requested resource
        /// </summary>
        [global::ProtoBuf.ServiceModel.ProtoComment("The identifier of the requested resource")]
        [global::ProtoBuf.ProtoMember(1, IsRequired = true, Name=@"resourceId", DataFormat = global::ProtoBuf.DataFormat.Default)]
        public string resourceId
        {
          get { return _resourceId; }
          set { _resourceId = value; }
        }
        private global::ProtoBuf.IExtension extensionObject;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
          { return global::ProtoBuf.Extensible.GetExtensionObject(ref extensionObject, createIfMissing); }
      }
    }
  
