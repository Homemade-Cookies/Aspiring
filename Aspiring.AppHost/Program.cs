using Aspiring.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

//seed mongodb into a storage container
//builder.AddDockerfile("spa", "spa/Dockerfile");
//builder.AddDockerfile("api", "api/Dockerfile");

var mongo = builder.AddMongoDB("MongoDB")
    .WithExternalHttpEndpoints()
    .WithOtlpExporter()
    .WithVolume("mongo-data", "/data/db")
    .WithMongoExpress();

var mongoDb = mongo.AddDatabase("MongoDB-Database");

var sqlDb = builder.AddSqlServer("sql")
    .WithExternalHttpEndpoints()
    .WithOtlpExporter()
    .WithVolume("sql-data", "/data/db")
    .AddDatabase("sql-Database");

var cache = builder.AddRedis("cache").WithRedisInsight().PublishAsContainer();

var api = builder.AddProject<Projects.Aspiring_ApiService>("AspiringAPI")
    .WithExternalHttpEndpoints()
    .WaitFor(mongoDb)
    .WaitFor(cache)
    .WithReference(cache)
    .WithReference(mongoDb);

var sqlApi = builder.AddProject<Projects.Aspiring_ApiService_Sql>("AspiringAPI-SQL")
    .WithExternalHttpEndpoints()
    .WaitFor(sqlDb)
    .WaitFor(cache)
    .WithReference(sqlDb)
    .WithReference(cache);

var grafana = builder.AddContainer("Grafana", "grafana/grafana")
                     .WithBindMount("../grafana/config", "/etc/grafana", isReadOnly: true)
                     .WithBindMount("../grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
                     .WithHttpEndpoint(targetPort: 3000, name: "http");

//builder.AddProject<Projects.Aspiring_Web>("app")
//       .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"));

var spa = builder.AddProject<Projects.Aspiring_Web>("AspiringWeb")
    .WithExternalHttpEndpoints()
    .WaitFor(api)
    .WithReference(cache)
    .WithReference(api)
    .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"));

builder.AddHealthChecksUI("Health-Checks-UI")
    .WithReference(api)
    .WithReference(sqlApi)
    .WithReference(spa)
    // This will make the HealthChecksUI dashboard available from external networks when deployed.
    // In a production environment, you should consider adding authentication to the ingress layer
    // to restrict access to the dashboard.
    .WithExternalHttpEndpoints();

builder.AddContainer("Prometheus", "prom/prometheus")
       .WithBindMount("../prometheus", "/etc/prometheus", isReadOnly: true)
       .WithHttpEndpoint(/* This port is fixed as it's referenced from the Grafana config */ port: 9090, targetPort: 9090);

builder.Build().Run();
