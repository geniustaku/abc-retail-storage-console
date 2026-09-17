// ABC Retail - Azure Functions
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
