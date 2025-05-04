namespace Froola.Interfaces;

public interface IFroolaMergeConfig
{
}

public interface IFroolaMergeConfig<out TConfig> : IFroolaMergeConfig where TConfig : IFroolaMergeConfig
{
    TConfig Build();
}