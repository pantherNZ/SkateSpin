using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class TrickSelectorPage : IBasePage, ISavableComponent, IEventReceiver
{
    private readonly List<DataHandler.TrickEntry> currentTrickPool = new List<DataHandler.TrickEntry>();
    private List<DataHandler.TrickEntry> currentTrickList = new List<DataHandler.TrickEntry>();
    private bool trickPoolDirty = true;

    private List<string> _currentCategories = new List<string>();
    public ReadOnlyCollection<string> CurrentCategories
    {
        get { return _currentCategories.AsReadOnly(); }
    }

    [SerializeField] private Text currentTrickText = null;
    [SerializeField] private Text difficultyText = null;
    [SerializeField] private MinMaxSlider difficultySlider = null;
    private int index;

    public class LandData
    {
        public int landed;
        public int total;
        public Dictionary<int, Pair<int, int>> perDifficultyLands;
    }

    Dictionary<string, LandData> _landedData = new Dictionary<string, LandData>();
    public ReadOnlyDictionary<string, LandData> LandedData
    {
        get { return GetLandedData(); }
    }
    private bool landedDataDirty = true;

    private void Awake()
    {
        ResetSaveData();
        EventSystem.Instance.AddSubscriber( this );
        SaveGameSystem.AddSaveableComponent( this );
        difficultySlider.OnValueSet += ( min, max ) => trickPoolDirty = true;
    }

    void Initialise()
    {
        // Setup trick list
        RecalculateCurrentTrickList();
        RandomiseTrickList();
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e.GetType() == typeof( UseShortTrickNamesEvent ) && currentTrickList.Count > 0 )
            UpdateCurrentTrick( ( ( UseShortTrickNamesEvent )e ).value );
        else if( e.GetType() == typeof( DataLoadedEvent ) )
            Initialise();
        else if( e.GetType() == typeof( ResetSaveDataEvent ) )
            ResetSaveData();
    }

    public void RecalculateCurrentTrickList()
    {
        if( !trickPoolDirty )
            return;

        currentTrickPool.Clear();

        // Pull difficulty from slider
        int minDifficulty = Mathf.RoundToInt( difficultySlider.GetMinValue() );
        int maxDifficulty = Mathf.RoundToInt( difficultySlider.GetMaxValue() );

        foreach( var trick in DataHandler.Instance.TrickData )
        {
            if( trick.difficulty < minDifficulty || trick.difficulty > maxDifficulty )
                continue;

            if( !_currentCategories.Contains( trick.category ) )
                continue;

            if( trick.status == DataHandler.TrickEntry.Status.Banned )
                continue;

            if( trick.status == DataHandler.TrickEntry.Status.Landed && !AppSettings.Instance.canPickLandedTricks )
                continue;

            currentTrickPool.Add( trick );
        }

        if( currentTrickPool.IsEmpty() )
        {
            Debug.LogError( string.Format( "RecalculateCurrentTrickList resulted in 0 entries (difficulty: {0}-{1}, categories: {2})"
                , difficultySlider.GetMinValue()
                , difficultySlider.GetMaxValue()
                , string.Join( ", ", CurrentCategories ) ) );
        }

        trickPoolDirty = false;
    }

    private ReadOnlyDictionary<string, LandData> GetLandedData()
    {
        if( landedDataDirty )
        {
            _landedData.Clear();

            foreach( var category in DataHandler.Instance.Categories )
            {
                var newData = new LandData
                {
                    landed = 0,
                    total = 0,
                    perDifficultyLands = new Dictionary<int, Pair<int, int>>()
                };

                foreach( var( difficulty, _ ) in DataHandler.Instance.DifficultyNames )
                    newData.perDifficultyLands[difficulty] = new Pair<int, int>( 0, 0 );

                _landedData.Add( category, newData );
            }

            foreach( var trick in DataHandler.Instance.TrickData )
            {
                if( _landedData.TryGetValue( trick.category, out var current ) )
                {
                    current.landed += trick.status == DataHandler.TrickEntry.Status.Landed ? 1 : 0;
                    current.total++;
                    if( current.perDifficultyLands.TryGetValue( trick.difficulty, out var oldValue ) )
                        current.perDifficultyLands[trick.difficulty] = new Pair<int, int>( oldValue.First + ( trick.status == DataHandler.TrickEntry.Status.Landed ? 1 : 0 ), oldValue.Second + 1 );
                }
            }

            landedDataDirty = false;
        }

        return new ReadOnlyDictionary<string, LandData>( _landedData );
    }

    public void ToggleCategory( Toggle toggle, string category )
    {
        if( !DataHandler.Instance.Categories.Contains( category ) )
        {
            Debug.LogError( "Invalid cateogry specified: '" + category + "' from " + toggle.name );
            return;
        }

        if( toggle.isOn && !_currentCategories.Contains( category ) )
        {
            _currentCategories.Add( category );
        }
        else if( !toggle.isOn && _currentCategories.Contains( category ) )
        {
            // Don't allow disabling the last category
            if( _currentCategories.Count == 1 )
            {
                toggle.SetIsOnWithoutNotify( !toggle.isOn );
                return;
            }

            _currentCategories.Remove( category );
        }

        trickPoolDirty = true;
        DataHandler.Instance.Save();
    }

    private void UpdateCurrentTrick( bool useShortTrickNames )
    {
        if( currentTrickList.IsEmpty() )
            return;

        var displayName = currentTrickList[index].name;
        
        //if( AppSettings.Instance.alternateTrickNamesEnabled )
        //   displayName += ( currentTrickList[index].secondaryName.Length > 0 ? "\n(" + currentTrickList[index].secondaryName + ")" : string.Empty );

        if( useShortTrickNames )
            foreach( var( toReplace, replaceWith ) in DataHandler.Instance.ShortTrickNameReplacements )
                displayName = displayName.Replace( toReplace, replaceWith );

        currentTrickText.text = displayName;

        difficultyText.text = string.Format( "Difficulty - {0} ({1})", 
            currentTrickList[index].difficulty,
            DataHandler.Instance.DifficultyNames[currentTrickList[index].difficulty] );
    }

    public void NextTrick()
    {
        if( currentTrickList.Count == 0 )
            return;

        // TODO: Play animation / visual
        index = ( index + 1 ) % currentTrickList.Count;
        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
    }

    public void PreviousTrick()
    {
        if( currentTrickList.Count == 0 )
            return;

        // TODO: Play animation / visual
        index = ( index + currentTrickList.Count - 1 ) % currentTrickList.Count;
        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
    }

    public void RandomiseTrickList()
    {
        // TODO: Play animation / visual
        RecalculateCurrentTrickList();
        currentTrickList = new List<DataHandler.TrickEntry>( currentTrickPool ).RandomShuffle();
        index = 0;
        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
    }

    public void BanCurrentTrick()
    {
        if( currentTrickList.Count == 0 )
            return;

        // TODO: Play animation / visual
        currentTrickList[index].status = DataHandler.TrickEntry.Status.Banned;
        NextTrick();
        DataHandler.Instance.Save();
    }

    public void LandCurrentTrick()
    {
        if( currentTrickList.Count == 0 )
            return;

        // TODO: Play animation / visual
        currentTrickList[index].status = DataHandler.TrickEntry.Status.Landed;
        landedDataDirty = true;
        NextTrick();
        DataHandler.Instance.Save();
    }

    private void ResetSaveData()
    {
        _currentCategories = new List<string> { "Flat Ground" };
        difficultySlider.SetMinValue( 1.0f );
        difficultySlider.SetMaxValue( 10.0f );
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        writer.Write( _currentCategories.Count );
        writer.Write( difficultySlider.GetMinValue() );
        writer.Write( difficultySlider.GetMaxValue() );

        foreach( var category in _currentCategories )
            writer.Write( category );
    }

    void ISavableComponent.Deserialise( BinaryReader reader )
    {
        _currentCategories.Clear();
        var count = reader.ReadInt32();
        var minDifficulty = reader.ReadSingle();
        var maxDifficulty = reader.ReadSingle();

        difficultySlider.SetMinValue( minDifficulty );
        difficultySlider.SetMaxValue( maxDifficulty );

        for( int i = 0; i < count; ++i )
            _currentCategories.Add( reader.ReadString() );
    }
}
