using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text;

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

    private int challengeTrickIndex;
    private DataHandler.ChallengeData currentChallenge;

    [SerializeField] private RectTransform root = null;
    [SerializeField] private RectTransform trickDisplay = null;
    [SerializeField] private Text currentTrickText = null;
    [SerializeField] private Text difficultyText = null;
    [SerializeField] private MinMaxSlider difficultySlider = null;
    [SerializeField] private Button nextButton = null;
    [SerializeField] private Button previousButton = null;
    [SerializeField] private Image bannedDisplay = null;
    [SerializeField] private Image landedDisplay = null;
    [SerializeField] private GameObject trickInfoButton = null;
    [SerializeField] private GameObject selectorButtonsPanel = null;
    [SerializeField] private GameObject challengeButtonsPanel = null;
    [SerializeField] private GameObject optionsPanel = null;
    [SerializeField] private GameObject menuButton = null;
    [SerializeField] private Text challengeInfoText = null;
    [SerializeField] private Text optionsText = null;
    [SerializeField] private Button increaseDifficultyButton = null;
    [SerializeField] private Button decreaseDifficultyButton = null;
    [SerializeField] private Text landedTrickText = null;
    private int index;
    private bool showAlternateTrickName;
    private bool challengeMode;

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
    public void SetTrickStatus( DataHandler.TrickEntry trick, DataHandler.TrickEntry.Status status, bool incrementLands, bool saveInstant )
    {
        if( trick.status == status )
            return;

        trick.status = status;

        if( incrementLands )
            trick.lands++;

        landedDataDirty = true;
        if( status == DataHandler.TrickEntry.Status.Landed )
            EventSystem.Instance.TriggerEvent( new TrickLandedEvent() { trick = trick } );
        DataHandler.Instance.Save( saveInstant );
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

        if( challengeMode )
        {
            var savedIdx = challengeTrickIndex;
            challengeMode = false;
            ActivateChallenge( currentChallenge );
            challengeTrickIndex = savedIdx;
            UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
        }
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is UseShortTrickNamesEvent trickEvent && currentTrickList.Count > 0 )
            UpdateCurrentTrick( trickEvent.value );
        else if( e is DataLoadedEvent )
            Initialise();
        else if( e is ResetSaveDataEvent )
        {
            ResetSaveData();
            Initialise();
        }
        else if( e is StartChallengeRequestEvent startChallengeRequest )
            ActivateChallenge( startChallengeRequest.challenge );
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

            if( !trick.canBeRolled )
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

        toggle.GetComponentInChildren<Text>().color = toggle.isOn ? new Color( 0XE6 / 255.0f, 0XEE / 255.0f, 0XF8 / 255.0f ) : new Color( 0X98 / 255.0f, 0X98 / 255.0f, 0X98 / 255.0f );
        trickPoolDirty = true;
        DataHandler.Instance.Save( false );
    }

    private DataHandler.TrickEntry GetCurrentTrick()
    {
        if( challengeMode )
        {
            return currentChallenge.tricks[challengeTrickIndex];
        }
        else if( previousIndex > 0 )
        {
            return previousTrickList[previousTrickList.Count - previousIndex];
        }
        else if( index < currentTrickList.Count )
        {
            return currentTrickList[index];
        }

        return null;
    }

    private void UpdateCurrentTrick( bool useShortTrickNames )
    {
        var trickToUse = GetCurrentTrick();

        if( trickToUse == null )
            return;

        var displayName = showAlternateTrickName ? trickToUse.secondaryName : trickToUse.name;

        if( useShortTrickNames )
            foreach( var (toReplace, replaceWith) in DataHandler.Instance.ShortTrickNameReplacements )
                displayName = displayName.Replace( toReplace, replaceWith );
        currentTrickText.text = displayName;

        var displayText = string.Format( "{0}\nDifficulty {1} ({2})",
            DataHandler.Instance.CategoryDisplayNames[trickToUse.category],
            trickToUse.difficulty,
            DataHandler.Instance.DifficultyNames[trickToUse.difficulty] );

        if( challengeMode )
        {
            displayText += string.Format( "\nTrick {0}/{1}", challengeTrickIndex + 1, currentChallenge.tricks.Count );
            landedTrickText.text = currentChallenge.landedData.Get( challengeTrickIndex ) ? "Landed" : "Not landed";
        }
        else if( trickToUse.lands == 0 || trickToUse.status == DataHandler.TrickEntry.Status.Default )
        {
            landedTrickText.text = "Not landed";
        }
        else if( trickToUse.lands == 1 )
        {
            landedTrickText.text = "Landed once";
        }
        else
        {
            landedTrickText.text = string.Format( "Landed {0} times", trickToUse.lands );
        }

        difficultyText.text = displayText;
        trickInfoButton.SetActive( trickToUse.secondaryName.Length > 0 );

        increaseDifficultyButton.interactable = !challengeMode && trickToUse.difficulty < 10;
        decreaseDifficultyButton.interactable = !challengeMode && trickToUse.difficulty > 1;

        nextButton.gameObject.SetActive( challengeMode || previousIndex > 0 );
        previousButton.gameObject.SetActive( challengeMode || previousIndex < previousTrickList.Count );
    }

    public void NextTrick()
    {
        if( challengeMode )
        {
            challengeTrickIndex = Utility.Mod( challengeTrickIndex + 1, currentChallenge.tricks.Count );
            
        }
        else
        {
            if( currentTrickList.Count == 0 || previousIndex == 0 )
                return;

            --previousIndex;
        }

        showAlternateTrickName = false;
        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
    }

    public void PreviousTrick()
    {
        if( challengeMode )
        {
            challengeTrickIndex = Utility.Mod( challengeTrickIndex - 1, currentChallenge.tricks.Count );

        }
        else
        {
            if( currentTrickList.Count == 0 || previousIndex >= previousTrickList.Count )
                return;

            previousIndex++;
        }

        showAlternateTrickName = false;
        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
    }

    public void RandomiseTrickList()
    {
        if( challengeMode )
        {
            currentChallenge.landedData.Set( challengeTrickIndex, true );

            for( int i = 1; i < currentChallenge.tricks.Count; ++i )
            {
                var index = Utility.Mod( challengeTrickIndex + i, currentChallenge.tricks.Count );

                if( !currentChallenge.landedData.Get( index ) )
                {
                    challengeTrickIndex = index;
                    UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
                    return;
                }
            }

            DeactivateChallenge( true );
            return;
        }
        else
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
        }

        showAlternateTrickName = false;
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
        if( challengeMode )
        {
            DeactivateChallenge( false );
            return;
        }

        if( currentTrickList.Count == 0 )
            return;

        currentTrickList[index].status = DataHandler.TrickEntry.Status.Banned;
        DataHandler.Instance.Save( true );
        PlayBanLandAnimation( bannedDisplay.gameObject );
    }

    public void LandCurrentTrick()
    {
        if( challengeMode )
        {
            if( challengeTrickIndex >= currentChallenge.tricks.Count )
            {
                Debug.LogError( "LandCurrentTrick (challengeMode) - Invalid challenge trick index" );
                return;
            }

            SetTrickStatus( currentChallenge.tricks[challengeTrickIndex], DataHandler.TrickEntry.Status.Landed, true, true );
        }
        else 
        {
            if( index >= currentTrickList.Count )
            {
                Debug.LogError( "LandCurrentTrick (trick mode) - Invalid trick index" );
                return;
            }

            SetTrickStatus( currentTrickList[index], DataHandler.TrickEntry.Status.Landed, true, true );
        }

        PlayBanLandAnimation( landedDisplay.gameObject );
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

        Utility.FunctionTimer.CreateTimer( 1.5f, () =>
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
        challengeTrickIndex = 0;
        challengeMode = false;
    }

    public void ToggleAlternateTrickName()
    {
        if( Utility.FunctionTimer.GetTimer( "LandBanTimer" ) != null )
            return;

        showAlternateTrickName = !showAlternateTrickName;
        StartCoroutine( AlternateTrickInterpolate() );
    }

    public void ActivateChallenge( DataHandler.ChallengeData challenge )
    {
        if( !challengeMode )
        {
            ToggleChallengeMode();
            currentChallenge = challenge;
            challengeInfoText.text = string.Format( "{0}\n<size=50>Defend against {1}</size>", currentChallenge.name, currentChallenge.person );
            UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
        }
    }

    public void DeactivateChallenge( bool completed )
    {
        if( challengeMode )
        {
            EventSystem.Instance.TriggerEvent( new ChallengeEndedEvent() { challenge = currentChallenge, completed = completed } );
            ToggleChallengeMode();
            EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = 3 } );
            UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
        }
    }

    private void ToggleChallengeMode()
    {
        challengeMode = !challengeMode;
        currentChallenge = null;
        challengeTrickIndex = 0;
        optionsPanel.ToggleActive();
        selectorButtonsPanel.ToggleActive();
        challengeButtonsPanel.ToggleActive();
        menuButton.ToggleActive();
        challengeInfoText.gameObject.ToggleActive();
        optionsText.gameObject.ToggleActive();
        ( trickDisplay.parent as RectTransform ).anchoredPosition = new Vector2( 0.0f, challengeMode ? -135.0f : 0.0f );
        FindObjectOfType<PageNavigator>().draggingEnabled = !challengeMode;
    }

    IEnumerator AlternateTrickInterpolate()
    {
        var interp = 0.0f;
        var durationSec = 1.0f;

        while( interp < Mathf.PI * 2.0f )
        {
            var prev = interp;
            interp += Time.deltaTime * ( Mathf.PI * 2.0f / durationSec );
            trickDisplay.localScale = new Vector3( 1.0f, 0.5f + Mathf.Cos( interp ) / 2.0f, 1.0f );

            if( prev < Mathf.PI && interp >= Mathf.PI )
                UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );

            yield return null;
        }

        trickDisplay.localScale = new Vector3( 1.0f, 1.0f, 1.0f );

        //yield return StartCoroutine( Utility.InterpolateScale( trickDisplay, new Vector3( 1.0f, 0.0f, 1.0f ), 0.3f ) );
        //yield return new WaitForSeconds( 0.1f );
        //yield return StartCoroutine( Utility.InterpolateScale( trickDisplay, new Vector3( 1.0f, 1.0f, 1.0f ), 0.3f ) );
    }

    public void ModifyCurrentTrickDifficulty( bool increase )
    {
        if( GetCurrentTrick().status == DataHandler.TrickEntry.Status.Landed )
            landedDataDirty = true;

        DataHandler.Instance.ModifyTrickDifficulty( GetCurrentTrick(), increase );
        UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        writer.Write( _currentCategories.Count );
        writer.Write( difficultySlider.GetMinValue() );
        writer.Write( difficultySlider.GetMaxValue() );
        writer.Write( challengeMode );

        if( challengeMode )
        {
            writer.Write( ( char )challengeTrickIndex );
            writer.Write( currentChallenge.hash );
            writer.Write( ( char )currentChallenge.index );
        }

        foreach( var category in _currentCategories )
            writer.Write( category );
    }

    void ISavableComponent.Deserialise( BinaryReader reader )
    {
        _currentCategories.Clear();
        var count = reader.ReadInt32();
        var minDifficulty = reader.ReadSingle();
        var maxDifficulty = reader.ReadSingle();
        challengeMode = reader.ReadBoolean();

        if( challengeMode )
        {
            challengeTrickIndex = reader.ReadChar();
            var hash = reader.ReadUInt32();
            var index = reader.ReadChar();

            if( !DataHandler.Instance.ChallengesData.ContainsKey( hash ) )
            {
                Debug.LogError( "Deserialising challenge hash failed to find a valid challenge entry" );
                challengeMode = false;
            }
            else
            {
                currentChallenge = DataHandler.Instance.ChallengesData[hash][index];
            }
        }

        difficultySlider.SetMinValue( minDifficulty );
        difficultySlider.SetMaxValue( maxDifficulty );

        for( int i = 0; i < count; ++i )
            _currentCategories.Add( reader.ReadString() );
    }
}