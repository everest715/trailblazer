using System.Collections.Generic;
using System.Threading.Tasks;

public interface IPicoFloorAreaProvider
{
    Task<IReadOnlyList<PicoFloorArea>> GetFloorAreasAsync();
}
