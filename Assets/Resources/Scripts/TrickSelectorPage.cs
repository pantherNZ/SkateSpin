using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class TrickSelectorPage : IBasePage, ISavableComponent, IEventReceiver
{
    private readonly List<DataHandler.TrickEntry> currentTrickPool = new List<DataHandler.TrickEntry>();
    private List<DataHandler.TrickEntry> currentTrickList = new List<DataHandler.TrickEntry>();
    private List<DataHandler.TrickEntry> previousTrickList = new List<DataHandler.TrickEntry>();
    private int previousIndex;
    private bool trickPoolDirty = true;

    private List<string> _currentCategories = new List<string>();

    public ReadOnlyCollection<string> CurrentCategories
    {
        get { return _currentCategories.AsReadOnly(); }
    }

    [SerializeField] private RectTransform root = null;
    [SerializeField] private Text currentTrickText = null;
    [SerializeField] private Text difficultyText = null;
    [SerializeField] private MinMaxSlider difficultySlider = null;
    [SerializeField] private Button nextButton = null;
    [SerializeField] private Button previousButton = null;
    [SerializeField] private Image bannedDisplay = null;
    [SerializeField] private Image landedDisplay = null;
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
    public void SetLandedDataDirty()
    {
        landedDataDirty = true;
        DataHandler.Instance.Save( false );
    }

    private void Awake()
    {
        ResetSaveData();
        EventSystem.Instance.AddSubscriber( this );
        SaveGameSystem.AddSaveableComponent( this );
        difficultySlider.OnValueSet += ( min, max ) => trickPoolDirty = true;
        previousButton.gameObject.SetActive( false );
        nextButton.gameObject.SetActive( false );
        bannedDisplay.gameObject.SetActive( false );
        landedDisplay.gameObject.SetActive( false );
    }

    void Initialise()
    {
        // Setup trick list
        RecalculateCurrentTrickList();
        RandomiseTrickList();
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is UseShortTrickNamesEvent trickEvent && currentTrickList.Count > 0 )
            UpdateCurrentTrick( trickEvent.value );
        else if( e is DataLoadedEvent )
            Initialise();
        else if( e is ResetSaveDataEvent )
            ResetSaveData();
    }

    public void RecalculateCurrentTrickList()
    {
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
        DataHandler.Instance.Save( false );
    }

    private void UpdateCurrentTrick( bool useShortTrickNames )
    {
        if( currentTrickList.IsEmpty() )
            return;

        var trickToUse = previousIndex > 0 ? previousTrickList[previousTrickList.Count - previousIndex] : currentTrickList[index];
        var displayName = trickToUse.name;
        
        //if( AppSettings.Instance.alternateTrickNamesEnabled )
        //   displayName += ( currentTrickList[index].secondaryName.Length > 0 ? "\n(" + currentTrickList[index].secondaryName + ")" : string.Empty );

        if( useShortTrickNames )
            foreach( var( toReplace, replaceWith ) in DataHandler.Instance.ShortTrickNameReplacements )
                displayName = displayName.Replace( toReplace, replaceWith );

        currentTrickText.text = displayName;

        difficultyText.text = string.Format( "Difficulty - {0} ({1})",
            trickToUse.difficulty,
            DataHandler.Instance.DifficultyNames[trickToUse.difficulty] );
    }

    public void NextTrick()
    {
        if( currentTrickList.Count == 0 || previousIndex == 0 )
            return;

        // TODO: Play animation / visual
        --previousIndex;
        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
        previousButton.gameObject.SetActive( true );

        if( previousIndex == 0 )
            nextButton.gameObject.SetActive( false );
    }

    public void PreviousTrick()
    {
        if( currentTrickList.Count == 0 || previousIndex >= previousTrickList.Count )
            return;

        previousIndex++;
        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
        nextButton.gameObject.SetActive( true );

        if( previousIndex >= previousTrickList.Count )
            previousButton.gameObject.SetActive( false );
    }

    public void RandomiseTrickList()
    {
        if( trickPoolDirty )
        {
            RecalculateCurrentTrickList();
            AppendPreviousTrick();
            currentTrickList = new List<DataHandler.TrickEntry>( currentTrickPool ).RandomShuffle();
        }
        else
        {
            AppendPreviousTrick();
            index++;
            previousIndex = 0;

            if( index >= currentTrickList.Count )
            {
                currentTrickList = new List<DataHandler.TrickEntry>( currentTrickPool ).RandomShuffle();
                index = 0;
            }
        }

        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
    }

    private void AppendPreviousTrick()
    {
        if( currentTrickList.Count == 0 )
            return;

        previousTrickList.Add( currentTrickList[index] );
        previousButton.gameObject.SetActive( true );

        if( previousTrickList.Count > 50 )
            previousTrickList.RemoveAt( 0 );
    }

    public void BanCurrentTrick()
    {
        if( currentTrickList.Count == 0 )
            return;

        currentTrickList[index].status = DataHandler.TrickEntry.Status.Banned;
        DataHandler.Instance.Save( true );
        PlayBanLandAnimation( bannedDisplay.gameObject );
    }

    public void LandCurrentTrick()
    {
        if( currentTrickList.Count == 0 )
            return;

        currentTrickList[index].status = DataHandler.TrickEntry.Status.Landed;
        DataHandler.Instance.Save( true );
        PlayBanLandAnimation( landedDisplay.gameObject );
        landedDataDirty = true;
    }

    public void PlayBanLandAnimation( GameObject visual )
    {
        var timerName = "LandBanTimer";
        if( Utility.FunctionTimer.GetTimer( timerName ) != null )
            return;

        visual.SetActive( true );
        visual.transform.localScale = new Vector3( 5.0f, 5.0f, 5.0f );

        float bannedTimer = 0.2f;
        StartCoroutine( Utility.InterpolateScale( visual.transform, new Vector3( 1.0f, 1.0f, 1.0f ), bannedTimer ) );

        Utility.FunctionTimer.CreateTimer( bannedTimer, () =>
        {
            StartCoroutine( Utility.Shake( root, 0.2f, 60.0f, 10.0f, 60.0f, 1.5f ) );
        } );

        Utility.FunctionTimer.CreateTimer( 2.0f, () =>
        {
            RandomiseTrickList();
            visual.SetActive( false );
        }, timerName );
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
