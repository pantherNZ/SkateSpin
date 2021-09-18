using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class TrickSelectorPage : IBasePage, ISavableComponent, IEventReceiver
{
    public List<DataHandler.TrickEntry> currentTrickList = new List<DataHandler.TrickEntry>();
    public List<string> currentCategories = new List<string>();
    
    [SerializeField] private Text currentTrickText = null;
    [SerializeField] private Text difficultyText = null;
    [SerializeField] private MinMaxSlider difficultySlider = null;
    [SerializeField] private DataHandler dataHandler = null;
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

    private bool allowLandedTricksToBeSelected;

    private void Start()
    {
        if( dataHandler.IsDataLoaded )
            Initialise();
        else
            dataHandler.OnDataLoaded += () => Initialise();

        dataHandler.OnResetSaveData += ResetSaveData;

        EventSystem.Instance.AddSubscriber( this );
    }

    void Initialise()
    {
        // Setup trick list
        RecalculateCurrentTrickList();

        // Initialise first trick
        NextTrick();
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e.GetType() == typeof( UseShortTrickNamesEvent ) ||
            e.GetType() == typeof( AlternateTrickNamesEvent ) )
        {
            if( currentTrickList.Count > 0 )
                UpdateCurrentTrick();
        }
    }

    public void RecalculateCurrentTrickList()
    {
        currentTrickList.Clear();

        // Pull difficulty from slider
        int minDifficulty = Mathf.RoundToInt( difficultySlider.GetMinValue() );
        int maxDifficulty = Mathf.RoundToInt( difficultySlider.GetMaxValue() );

        foreach( var trick in dataHandler.TrickData )
        {
            if( trick.difficulty < minDifficulty || trick.difficulty > maxDifficulty )
                continue;

            if( !currentCategories.Contains( trick.category ) )
                continue;

            if( trick.status == DataHandler.TrickEntry.Status.Banned )
                continue;

            if( trick.status == DataHandler.TrickEntry.Status.Landed && !allowLandedTricksToBeSelected )
                continue;

            currentTrickList.Add( trick );
        }

        RandomiseTrickList();
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

    bool callbackEnabled = true;

    public void ToggleCategory( Toggle toggle, string category )
    {
        if( !callbackEnabled )
            return;

        if( !dataHandler.Categories.Contains( category ) )
        {
            Debug.LogError( "Invalid cateogry specified: '" + category + "' from " + toggle.name );
            return;
        }

        if( toggle.isOn && !currentCategories.Contains( category ) )
        {
            currentCategories.Add( category );
        }
        else if( !toggle.isOn && currentCategories.Contains( category ) )
        {
            // Don't allow disabling the last category
            if( currentCategories.Count == 1 )
            {
                callbackEnabled = false;
                toggle.isOn = !toggle.isOn;
                callbackEnabled = true;
                return;
            }

            currentCategories.Remove( category );
        }

        dataHandler.Save();
    }

    private void UpdateCurrentTrick()
    {
        var displayName = currentTrickList[index].name;
        
        if( AppSettings.Instance.alternateTrickNamesEnabled )
            displayName += ( currentTrickList[index].secondaryName.Length > 0 ? "\n(" + currentTrickList[index].secondaryName + ")" : string.Empty );

        if( AppSettings.Instance.useShortTrickNames )
            foreach( var( toReplace, replaceWith ) in dataHandler.ShortTrickNameReplacements )
                displayName = displayName.Replace( toReplace, replaceWith );

        currentTrickText.text = displayName;

        difficultyText.text = string.Format( "Difficulty - {0} ({1})", 
            currentTrickList[index].difficulty, 
            dataHandler.DifficultyNames[currentTrickList[index].difficulty] );
    }

    public void NextTrick()
    {
        // TODO: Play animation / visual
        index = ( index + 1 ) % currentTrickList.Count;
        UpdateCurrentTrick();
       
    }

    public void PreviousTrick()
    {
        // TODO: Play animation / visual
        index = ( index + currentTrickList.Count - 1 ) % currentTrickList.Count;
        UpdateCurrentTrick();
    }

    public void RandomiseTrickList()
    {
        // TODO: Play animation / visual
        currentTrickList.RandomShuffle();
        UpdateCurrentTrick();
    }

    public void BanCurrentTrick()
    {
        // TODO: Play animation / visual
        currentTrickList[index].status = DataHandler.TrickEntry.Status.Banned;
        NextTrick();
        dataHandler.Save();
    }

    public void LandCurrentTrick()
    {
        // TODO: Play animation / visual
        currentTrickList[index].status = DataHandler.TrickEntry.Status.Landed;
        landedDataDirty = true;
        NextTrick();
        dataHandler.Save();
    }

    public void SetAllowLandedTricksToBeSelected( bool value )
    {
        allowLandedTricksToBeSelected = value;
        dataHandler.Save();
    }

    private void ResetSaveData()
    {
        allowLandedTricksToBeSelected = false;
        currentCategories = new List<string> { "Flat Ground" };
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        writer.Write( allowLandedTricksToBeSelected );
        writer.Write( currentCategories.Count );

        foreach( var category in currentCategories )
            writer.Write( category );
    }

    void ISavableComponent.Deserialise( BinaryReader reader )
    {
        allowLandedTricksToBeSelected = reader.ReadBoolean();
        var count = reader.ReadInt32();

        for( int i = 0; i < count; ++i )
            currentCategories.Add( reader.ReadString() );
    }
}
