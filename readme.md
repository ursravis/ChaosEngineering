1. docker-compose up -d
2. docker exec broker `
    kafka-topics --bootstrap-server localhost:9092 `
                --create `
                --topic my-topic
3. docker exec broker `
    kafka-topics --bootstrap-server localhost:9092 `
                --list            

4. docker exec --interactive --tty broker `
    kafka-console-producer --bootstrap-server broker:9092 `
                       --topic my-topic

5. docker exec --interactive --tty broker `
    kafka-console-consumer --bootstrap-server broker:9092 `
                       --topic my-topic `
                       --from-beginning