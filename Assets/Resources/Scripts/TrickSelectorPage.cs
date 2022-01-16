using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text;
using System;

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
    private int challengeTrickLives;
    private DataHandler.ChallengeData currentChallenge;

    [SerializeField] private RectTransform root = null;
    [SerializeField] private RectTransform trickDisplay = null;
    [SerializeField] private Text currentTrickText = null;
    [SerializeField] private Text difficultyText = null;
    [SerializeField] private MinMaxSlider difficultySlider = null;
    [SerializeField] private Button nextButton = null;
    [SerializeField] private Button previousButton = null;
    [SerializeField] private Image eventDisplay = null;
    [SerializeField] private Texture2D landedDisplay = null;
    [SerializeField] private Texture2D bannedDisplay = null;
    [SerializeField] private Texture2D missedDisplay = null;
    [SerializeField] private Texture2D completeDisplay = null;
    [SerializeField] private Texture2D lostDisplay = null;
    [SerializeField] private GameObject trickInfoButton = null;
    [SerializeField] private GameObject selectorButtonsPanel = null;
    [SerializeField] private GameObject challengeButtonsPanel = null;
    [SerializeField] private GameObject optionsPanel = null;
    [SerializeField] private GameObject menuButton = null;
    [SerializeField] private Text bailTrickButtonText = null;
    [SerializeField] private Text challengeInfoText = null;
    [SerializeField] private Text optionsText = null;
    [SerializeField] private GameObject quitChallengePanel = null;
    [SerializeField] private Text challengeLettersDisplay = null;
    [SerializeField] private Button increaseDifficultyButton = null;
    [SerializeField] private Button decreaseDifficultyButton = null;
    [SerializeField] private Text landedTrickText = null;
    private int currentTrickIndex;
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
        eventDisplay.gameObject.SetActive( false );
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
            ActivateChallenge( currentChallenge, challengeTrickLives );
            challengeTrickIndex = savedIdx;
            UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
        }
        else
        {
            challengeMode = true;
            ToggleChallengeMode();
        }
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is UseShortTrickNamesEvent trickEvent && currentTrickList.Count > 0 )
        {
            UpdateCurrentTrick( trickEvent.value );
        }
        else if( e is DataLoadedEvent )
        {
            Initialise();
        }
        else if( e is ResetSaveDataEvent )
        {
            ResetSaveData();
            Initialise();
        }
        else if( e is StartChallengeRequestEvent startChallengeRequest )
        {
            ActivateChallenge( startChallengeRequest.challenge, 5 );
        }
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

        toggle.GetComponentInChildren<Text>().color = toggle.isOn ? Utility.ColourFromHex( 0XE6, 0XEE, 0XF8 ) : Utility.ColourFromHex( 0X98, 0X98, 0X98 );
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
        else if( currentTrickIndex < currentTrickList.Count )
        {
            return currentTrickList[currentTrickIndex];
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

        increaseDifficultyButton.interactable = trickToUse.difficulty < 10;
        decreaseDifficultyButton.interactable = trickToUse.difficulty > 1;
        increaseDifficultyButton.gameObject.SetActive( !challengeMode );
        decreaseDifficultyButton.gameObject.SetActive( !challengeMode );

        nextButton.gameObject.SetActive( ( challengeMode && !currentChallenge.isGameOfSkate ) || previousIndex > 0 );
        previousButton.gameObject.SetActive( ( challengeMode && !currentChallenge.isGameOfSkate ) || previousIndex < previousTrickList.Count );
    }

    public void NextTrick()
    {
        if( challengeMode )
        {
            challengeTrickIndex = Utility.Mod( challengeTrickIndex + 1, currentChallenge.tricks.Count );
            DataHandler.Instance.Save( false );
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
            DataHandler.Instance.Save( false );
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

    private int FindNextUnlandedTrick()
    {
        for( int i = 0; i < currentChallenge.tricks.Count; ++i )
        {
            var index = Utility.Mod( challengeTrickIndex + i, currentChallenge.tricks.Count );
            if( !currentChallenge.landedData.Get( index ) )
                return index;
        }

        return -1;
    }

    public void RandomiseTrickList()
    {
        if( challengeMode )
        {
            int nextUnlanded = FindNextUnlandedTrick();

            if( nextUnlanded != -1 )
            {
                challengeTrickIndex = nextUnlanded;
                UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
                return;
            }

            PlayEventAnimation( completeDisplay, () => DeactivateChallenge( true ) );
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
                currentTrickIndex++;
                previousIndex = 0;

                if( currentTrickIndex >= currentTrickList.Count )
                {
                    currentTrickList = new List<DataHandler.TrickEntry>( currentTrickPool ).RandomShuffle();
                    currentTrickIndex = 0;
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

        previousTrickList.Add( currentTrickList[currentTrickIndex] );
        previousButton.gameObject.SetActive( true );

        if( previousTrickList.Count > 50 )
            previousTrickList.RemoveAt( 0 );
    }

    public void BanCurrentTrick()
    {
        if( Utility.FunctionTimer.GetTimer( "LandBanTimer" ) != null )
            return;

        if( currentTrickList.Count == 0 )
            return;

        currentTrickList[currentTrickIndex].status = DataHandler.TrickEntry.Status.Banned;
        DataHandler.Instance.Save( true );
        PlayEventAnimation( bannedDisplay, RandomiseTrickList );
    }

    public void LandCurrentTrick()
    {
        if( Utility.FunctionTimer.GetTimer( "LandBanTimer" ) != null )
            return;

        if( challengeMode )
        {
            if( challengeTrickIndex >= currentChallenge.tricks.Count )
            {
                Debug.LogError( "LandCurrentTrick (challengeMode) - Invalid challenge trick index" );
                return;
            }

            SetTrickStatus( currentChallenge.tricks[challengeTrickIndex], DataHandler.TrickEntry.Status.Landed, true, true );
            currentChallenge.landedData.Set( challengeTrickIndex, true );

            if( ( challengeTrickIndex == currentChallenge.tricks.Count - 1 && currentChallenge.isGameOfSkate ) || 
                ( currentChallenge.Completed && !currentChallenge.isGameOfSkate ) )
            {
                PlayEventAnimation( completeDisplay, () => DeactivateChallenge( true ) );
            }
            else
            {
                PlayEventAnimation( landedDisplay, RandomiseTrickList );
            }

            return;
        }

        if( currentTrickIndex >= currentTrickList.Count )
        {
            Debug.LogError( "LandCurrentTrick (trick mode) - Invalid trick index" );
            return;
        }

        SetTrickStatus( currentTrickList[currentTrickIndex], DataHandler.TrickEntry.Status.Landed, true, true );
        PlayEventAnimation( landedDisplay, RandomiseTrickList );
    }

    public void LoseChallengeLife()
    {
        if( Utility.FunctionTimer.GetTimer( "LandBanTimer" ) != null )
            return;

        if( !currentChallenge.isGameOfSkate )
        {
            DeactivateChallenge( false );
            return;
        }

        challengeTrickLives--;
        DataHandler.Instance.Save( false );
        UpdateChallengeLetters();

        if( challengeTrickLives == 0 )
        {
            PlayEventAnimation( lostDisplay, () => DeactivateChallenge( false ) );
        }
        else if( ( challengeTrickIndex == currentChallenge.tricks.Count - 1 && currentChallenge.isGameOfSkate ) )
        {
            PlayEventAnimation( completeDisplay, () => DeactivateChallenge( true ) );
        }
        else
        {
            PlayEventAnimation( missedDisplay, RandomiseTrickList );
        }
    }

    public void PlayEventAnimation( Texture2D texture, Action postAction = null )
    {
        var timerName = "LandBanTimer";
        if( Utility.FunctionTimer.GetTimer( timerName ) != null )
            return;

        eventDisplay.gameObject.SetActive( true );
        eventDisplay.transform.localScale = new Vector3( 5.0f, 5.0f, 5.0f );

        if( texture != null )
            eventDisplay.sprite = Utility.CreateSprite( texture );

        float bannedTimer = 0.2f;
        StartCoroutine( Utility.InterpolateScale( eventDisplay.transform, new Vector3( 1.0f, 1.0f, 1.0f ), bannedTimer ) );

        Utility.FunctionTimer.CreateTimer( bannedTimer, () =>
        {
            StartCoroutine( Utility.Shake( root, 0.2f, 60.0f, 10.0f, 60.0f, 1.5f ) );
        } );

        Utility.FunctionTimer.CreateTimer( 1.5f, () =>
        {
            postAction?.Invoke();
            eventDisplay.gameObject.SetActive( false );
        }, timerName );
    }

    private void ResetSaveData()
    {
        _currentCategories = new List<string> { "Flat Ground" };
        difficultySlider.SetMinValue( 1.0f );
        difficultySlider.SetMaxValue( 10.0f );
        challengeTrickIndex = 0;
        challengeMode = false;
        landedDataDirty = true;
    }

    public void ToggleAlternateTrickName()
    {
        if( Utility.FunctionTimer.GetTimer( "LandBanTimer" ) != null )
            return;

        showAlternateTrickName = !showAlternateTrickName;
        StartCoroutine( AlternateTrickInterpolate() );
    }

    public void ActivateChallenge( DataHandler.ChallengeData challenge, int lives )
    {
        if( !challengeMode )
        {
            ToggleChallengeMode( challenge, lives );
            bailTrickButtonText.text = currentChallenge.isGameOfSkate ? "Bail" : "Quit";
            challengeInfoText.text = string.Format( "{0}\n<size=50>{1}{2}</size>", currentChallenge.name, challenge.descriptionOverride.Length > 0 ? challenge.descriptionOverride : "Defend against ", currentChallenge.person );
            UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
        }
    }

    private void UpdateChallengeLetters()
    {
        challengeLettersDisplay.gameObject.SetActive( challengeMode && currentChallenge != null && currentChallenge.isGameOfSkate );
        var text = "S K A T E";
        challengeLettersDisplay.text = challengeMode ? text.Insert( Mathf.Max( 0, text.Length - challengeTrickLives * 2 ), "</color>" ).Insert( 0, "<color=#FF4F4FFF>" ) : text;
    }

    public void DeactivateChallenge( bool completed )
    {
        if( challengeMode )
        {
            // Game of skates don't track completions outside of the current attempt
            if( currentChallenge.isGameOfSkate && !completed )
            {
                currentChallenge.landedData.SetAll( false );
                DataHandler.Instance.Save( false );
            }

            EventSystem.Instance.TriggerEvent( new ChallengeEndedEvent() { challenge = currentChallenge, completed = completed } );
            ToggleChallengeMode();
            EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = 3 } );
            UpdateCurrentTrick( AppSettings.Instance.useShortTrickNames );
        }
    }

    private void ToggleChallengeMode( DataHandler.ChallengeData challenge = null, int lives = 0 )
    {
        challengeMode = !challengeMode;
        currentChallenge = challenge;
        challengeTrickLives = lives;
        challengeTrickIndex = ( challengeMode && challenge != null ) ? FindNextUnlandedTrick() : 0;

        optionsPanel.SetActive( !challengeMode );
        selectorButtonsPanel.SetActive( !challengeMode );
        menuButton.SetActive( !challengeMode );
        optionsText.gameObject.SetActive( !challengeMode );

        quitChallengePanel.SetActive( challengeMode );
        challengeInfoText.gameObject.SetActive( challengeMode );
        challengeButtonsPanel.SetActive( challengeMode );
        UpdateChallengeLetters();

        ( trickDisplay.parent as RectTransform ).anchoredPosition = new Vector2( 0.0f, challengeMode ? -150.0f : 0.0f );
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
            writer.Write( currentChallenge.hash );
            writer.Write( ( char )currentChallenge.index );
            writer.Write( ( char )challengeTrickLives );
        }

        foreach( var category in _currentCategories )
            writer.Write( category );
    }

    void ISavableComponent.Deserialise( int saveVersion, BinaryReader reader )
    {
        _currentCategories.Clear();
        var count = reader.ReadInt32();
        var minDifficulty = reader.ReadSingle();
        var maxDifficulty = reader.ReadSingle();
        challengeMode = reader.ReadBoolean();

        if( challengeMode )
        {
            var hash = reader.ReadUInt32();
            var index = reader.ReadChar();
            challengeTrickLives = reader.ReadChar();

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