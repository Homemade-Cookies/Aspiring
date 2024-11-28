using Aspiring.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

//seed mongodb into a storage container
//builder.AddDockerfile("spa", "spa/Dockerfile");
//builder.AddDockerfile("api", "api/Dockerfile");

var mongo = builder.AddMongoDB("MongoDB")
.WithMongoExpress(c =>
    c.WithHostPort(3022)
    .WithExternalHttpEndpoints()
    .WithEndpoint(8081, 3022)
    .WithOtlpExporter()
    .WithVolume("mongo-data", "/data/db")
);

var mongoDb = mongo.AddDatabase("MongoDB-Database");

var cache = builder.AddRedis("cache").WithRedisInsight().PublishAsContainer();

var api = builder.AddProject<Projects.Aspiring_ApiService>("AspiringAPI")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WithReference(mongoDb);

var grafana = builder.AddContainer("Grafana", "grafana/grafana")
                     .WithBindMount("../grafana/config", "/etc/grafana", isReadOnly: true)
                     .WithBindMount("../grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
                     .WithHttpEndpoint(targetPort: 3000, name: "http");

//builder.AddProject<Projects.Aspiring_Web>("app")
//       .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"));

var spa = builder.AddProject<Projects.Aspiring_Web>("AspiringWeb")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WithReference(api)
    .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"));

var chessGame = builder.AddProject<Projects.ChessGame>("ChessGame")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WithReference(api)
    .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"));

builder.AddHealthChecksUI("Health-Checks-UI")
    .WithReference(api)
    .WithReference(spa)
    .WithReference(chessGame)
    // This will make the HealthChecksUI dashboard available from external networks when deployed.
    // In a production environment, you should consider adding authentication to the ingress layer
    // to restrict access to the dashboard.
    .WithExternalHttpEndpoints();


builder.AddContainer("Prometheus", "prom/prometheus")
       .WithBindMount("../prometheus", "/etc/prometheus", isReadOnly: true)
       .WithHttpEndpoint(/* This port is fixed as it's referenced from the Grafana config */ port: 9090, targetPort: 9090);

builder.Build().Run();
