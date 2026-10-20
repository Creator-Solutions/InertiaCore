using InertiaCore.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var vite = builder.AddInertiaViteApp("vite", "../frontend");
var ssr = builder.AddInertiaSsrApp("ssr", "../frontend");

builder.AddProject<Projects.InertiaCore_Sample_Web>("web")
    .WithInertiaReferences(vite, ssr);

builder.Build().Run();
