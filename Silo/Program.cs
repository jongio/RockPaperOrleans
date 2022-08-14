using Orleans;
using Orleans.Hosting;
using RockPaperOrleans.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleans((context, siloBuilder) =>
{
    siloBuilder
        .UseDashboard(dashboardOptions => dashboardOptions.HostSelf = false)
        .HostInAzure(context.Configuration)
            .UseCosmosDbClustering()
            .UseCosmosDbGrainStorage();
});

builder.Services.AddServicesForSelfHostedDashboard();
builder.Services.AddHostedService<GameEngine>();

var app = builder.Build();
app.UseOrleansDashboard();

app.Run();

// the game engine will host the game engine grain
public class GameEngine : BackgroundService
{
    public GameEngine(IGrainFactory grainFactory, ILogger<GameEngine> logger)
    {
        GrainFactory = grainFactory;
        Logger = logger;
    }

    public IGrainFactory GrainFactory { get; set; }
    public ILogger<GameEngine> Logger { get; set; }
    public IGameGrain CurrentGameGrain { get; set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var newGame = () => CurrentGameGrain = GrainFactory.GetGrain<IGameGrain>(Guid.NewGuid());

        while (!stoppingToken.IsCancellationRequested)
        {
            // start a new game if we don't have one yet
            if (CurrentGameGrain == null) newGame();

            var currentGame = await CurrentGameGrain.GetGame();

            // select players if they're unselected so far
            if (currentGame.Player1 == null && currentGame.Player2 == null)
            {
                await CurrentGameGrain.SelectPlayers();
            }
            else
            {
                if(currentGame.Rounds > currentGame.Turns.Count)
                {
                    await CurrentGameGrain.Go();
                }
                else
                {
                    await CurrentGameGrain.Score();
                    newGame();
                }
            }

            await Task.Delay(1000);
        }
    }
}
