﻿public class UseShortTrickNamesEvent : IBaseEvent { public bool value; }
public class CanPickLandedTricksEvent : IBaseEvent { public bool value; }
public class ResetSaveDataEvent : IBaseEvent { }
public class DataLoadedEvent : IBaseEvent { }
public class TrickLandedEvent : IBaseEvent { public DataHandler.TrickEntry trick; }