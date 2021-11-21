﻿public class UseShortTrickNamesEvent : IBaseEvent { public bool value; }
public class CanPickLandedTricksEvent : IBaseEvent { public bool value; }
public class ResetSaveDataEvent : IBaseEvent { }
public class DataLoadedEvent : IBaseEvent { }
public class TrickLandedEvent : IBaseEvent { public DataHandler.TrickEntry trick; }
public class PageChangeRequestEvent : IBaseEvent { public int page; }
public class StartChallengeRequestEvent : IBaseEvent { public DataHandler.ChallengeData challenge; }
public class ChallengeTrickCompletedEvent : IBaseEvent { public DataHandler.ChallengeData challenge; }
public class ChallengeCompletedEvent : IBaseEvent { public DataHandler.ChallengeData challenge; }
public class TrickDifficultyChangedEvent : IBaseEvent { public DataHandler.TrickEntry trick; public int previousDifficulty; }