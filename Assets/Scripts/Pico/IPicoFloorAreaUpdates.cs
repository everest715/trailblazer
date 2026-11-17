using System;
using System.Collections.Generic;

public interface IPicoFloorAreaUpdates
{
    event Action<IReadOnlyList<PicoFloorArea>> AreasUpdated;
}
