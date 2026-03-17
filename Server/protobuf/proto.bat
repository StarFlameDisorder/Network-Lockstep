protoc -I=./proto --cpp_out=./output ./proto/*.proto
protoc -I=./proto --csharp_out=../../Assets/Protobuf ./proto/*.proto