package main

import (
	"context"
	"log"
	"net"

	"google.golang.org/grpc"
	"google.golang.org/grpc/metadata"

	hellov1 "spike/backend/gen/taakht/hello/v1"
)

type server struct{ hellov1.UnimplementedHelloServiceServer }

func (s *server) GetGreeting(ctx context.Context, req *hellov1.GetGreetingRequest) (*hellov1.GetGreetingResponse, error) {
	md, _ := metadata.FromIncomingContext(ctx)
	log.Printf("SEEN x-user-id=%v", md.Get("x-user-id"))
	return &hellov1.GetGreetingResponse{Message: "Hello " + req.GetName()}, nil
}

func main() {
	lis, err := net.Listen("tcp", ":9090")
	if err != nil {
		log.Fatal(err)
	}
	g := grpc.NewServer()
	hellov1.RegisterHelloServiceServer(g, &server{})
	log.Fatal(g.Serve(lis))
}
