::protoc --descriptor_set_out=ExtModule.protobin ExtModule.proto
::protogen ExtModule.protobin
protogen -i:ExtModule.proto -o:ExtModule.cs
@pause