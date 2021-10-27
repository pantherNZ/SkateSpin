using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ChallengesPage : IBasePage, IEventReceiver
{
    [SerializeField] VerticalLayoutGroup verticalLayout = null;
    [SerializeField] GameObject categoryHeadingPrefab = null;
    [SerializeField] GameObject challengeEntryPrefab = null;
    [SerializeField] GameObject challengeSubEntryPrefab = null;
    [SerializeField] Dropdown restrictionDropDown = null;
    [SerializeField] InputField filter = null;

    public class DefenderData
    {
        public DataHandler.ChallengeData entry;
        public GameObject uiElement;
        public Text text;
        public bool isVisibleFromRestriction;
        public bool isVisibleFromFilter;
    }

    public class ChallengeData
    {
        public bool wasOpen;
        public bool isOpen;
        public Button button;
        public GameObject uiElement;
        public List<DefenderData> defenders = new List<DefenderData>();
    }

    public class CategoryData
    {
        public Button categoryEntry;
        public bool isOpen;
        public bool anyChildVisible;
        public List<ChallengeData> challenges = new List<ChallengeData>();
    }

    private Dictionary<DataHandler.ChallengeData, GameObject> challengeToUIEntry = new Dictionary<DataHandler.ChallengeData, GameObject>();
    readonly Dictionary<string, CategoryData> categoryData = new Dictionary<string, CategoryData>();
    private bool callbackEnabled = true;

    void Awake()
    {
        EventSystem.Instance.AddSubscriber( this );

        restrictionDropDown.onValueChanged.AddListener( ( x ) => FilterEntries( true, false ) );
        filter.onValueChanged.AddListener( ( x ) => FilterEntries( false, true ) );
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is DataLoadedEvent )
        {
            Initialise();
        }
        else if( e is ChallengeCompletedEvent challengeCompleted )
        {
            UpdateCompleted( challengeCompleted.challenge );
        }
    }

    private void Initialise()
    {
        foreach( var (hash, challenges) in DataHandler.Instance.ChallengesData )
        {
            var entry = new ChallengeData(); 
            categoryData.GetOrAdd( challenges[0].category ).challenges.Add( entry );

            foreach( var challenge in challenges )
                entry.defenders.Add( new DefenderData() { entry = challenge } );
        }

        foreach( var( category, categoryInfo) in categoryData )
        {
            var categoryHeading = Instantiate( categoryHeadingPrefab );
            categoryHeading.transform.SetParent( verticalLayout.transform, false );
            categoryHeading.GetComponentInChildren<Text>().text = category;
            var categoryButton = categoryHeading.GetComponent<Button>();

            categoryInfo.categoryEntry = categoryButton;
            var buttons = new List<Button>();

            foreach( var challenge in categoryInfo.challenges )
            {
                var entry = Instantiate( challengeEntryPrefab );
                entry.GetComponentInChildren<Text>().text = challenge.defenders[0].entry.name;
                entry.transform.SetParent( verticalLayout.transform );
                buttons.Add( entry.GetComponent<Button>() );

                challenge.uiElement = entry;
                challenge.button = buttons.Back();
                challenge.wasOpen = true;

                foreach( var defender in challenge.defenders )
                {
                    var subEntry = Instantiate( challengeSubEntryPrefab );
                    var text = subEntry.GetComponentInChildren<Text>();
                    text.text = "Defend against " + defender.entry.person;
                    subEntry.transform.SetParent( verticalLayout.transform );

                    defender.text = text;
                    defender.uiElement = subEntry;

                    challengeToUIEntry.Add( defender.entry, subEntry );
                    UpdateCompleted( defender.entry );
                }

                buttons.Back().onClick.AddListener( () =>
                {
                    if( callbackEnabled )
                        challenge.wasOpen = !challenge.wasOpen;

                    challenge.isOpen = !challenge.isOpen;
                    foreach( var defender in challenge.defenders )
                        defender.uiElement.SetActive( challenge.isOpen && defender.isVisibleFromRestriction && defender.isVisibleFromFilter );
                } );
            }

            categoryButton.onClick.AddListener( () =>
            {
                if( !categoryInfo.anyChildVisible )
                    return;

                categoryInfo.isOpen = !categoryInfo.isOpen;

                // Read and open any difficulty categories already open
                if( categoryInfo.isOpen )
                {
                    callbackEnabled = false;

                    foreach( var (difficultyData, button) in Utility.Zip( categoryInfo.challenges, buttons ) )
                        if( difficultyData.wasOpen )
                            button.onClick.Invoke();

                    callbackEnabled = true;
                }

                foreach( var challenge in categoryInfo.challenges )
                {
                    bool anyTrickVisible = false;
                    bool categoryAndDifficultyOpen = ( challenge.isOpen || challenge.wasOpen ) && categoryInfo.isOpen;

                    foreach( var trick in challenge.defenders )
                    {
                        bool isVisible = trick.isVisibleFromRestriction && trick.isVisibleFromFilter;
                        anyTrickVisible |= isVisible;
                        trick.uiElement.SetActive( categoryAndDifficultyOpen && isVisible );
                    }

                    challenge.uiElement.SetActive( categoryInfo.isOpen && anyTrickVisible );
                    challenge.isOpen = categoryAndDifficultyOpen;
                }
            } );
        }

        FilterEntries( true, true );
    }

    private void UpdateCompleted( DataHandler.ChallengeData challenge )
    {
        var thisChallenge = challenge;
        var subEntry = challengeToUIEntry[thisChallenge];
        var strikethrough = subEntry.GetComponentInChildren<Image>( true );
        var text = subEntry.GetComponentInChildren<Text>();
        strikethrough.gameObject.SetActive( thisChallenge.completed );
        subEntry.GetComponentInChildren<Button>().onClick.RemoveAllListeners();

        text.color = challenge.completed ? new Color( 1.0f, 93.0f / 255.0f, 93.0f / 255.0f ) : Color.white;

        if( thisChallenge.completed )
        {
            strikethrough.transform.localScale = strikethrough.transform.localScale.SetX( Utility.GetTextWidth( text ) / 100.0f );
        }
        else
        {
            subEntry.GetComponentInChildren<Button>().onClick.AddListener( () =>
            {
                EventSystem.Instance.TriggerEvent( new StartChallengeRequestEvent() { challenge = thisChallenge } );
                EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = 0 } );
            } );
        }
    }

    public void FilterEntries( bool updateRestriction, bool updateFilter )
    {
        var restrictionOption = restrictionDropDown.options[restrictionDropDown.value].text;
        var filterLowercase = filter.text.ToLower();
        bool filterLanded = restrictionOption == "Complete";
        bool filterUnlanded = restrictionOption == "Incomplete";

        if( ( filterLowercase.Length > 0 || restrictionDropDown.value != 0 ) && !categoryData.Any( ( data ) => data.Value.isOpen ) )
            CollapseOrExpandAllEntries( false );

        foreach( var( category, data ) in categoryData )
        {
            data.anyChildVisible = false;

            foreach( var( difficulty, challenge ) in Utility.Enumerate( data.challenges ) )
            {
                bool anyVisible = false;

                foreach( var defender in challenge.defenders )
                {
                    if( updateRestriction )
                        defender.isVisibleFromRestriction = ( filterLanded && defender.entry.completed )
                            || ( filterUnlanded && !defender.entry.completed )
                            || restrictionDropDown.value == 0;

                    if( updateFilter )
                        defender.isVisibleFromFilter = filter.text.Length == 0
                            || defender.entry.name.ToLower().Contains( filterLowercase )
                            || defender.entry.person.ToLower().Contains( filterLowercase );

                    bool isVisible = defender.isVisibleFromRestriction && defender.isVisibleFromFilter;
                    anyVisible |= isVisible;
                    defender.uiElement.SetActive( challenge.isOpen && isVisible );
                }

                data.anyChildVisible |= anyVisible;
                challenge.uiElement.SetActive( data.isOpen && challenge.isOpen && anyVisible );
            }
        }
    }

    public void ToggleAllEntries()
    {
        CollapseOrExpandAllEntries( categoryData.Any( ( data ) => data.Value.isOpen ) );
    }

    public void CollapseOrExpandAllEntries( bool collapse )
    {
        callbackEnabled = false;

        foreach( var (_, data) in categoryData )
        {
            for( int i = 0; i < data.challenges.Count; ++i )
            {
                data.challenges[i].wasOpen = !collapse;
                if( data.challenges[i].isOpen == collapse )
                    data.challenges[i].button.onClick.Invoke();
            }

            if( data.isOpen == collapse )
                data.categoryEntry.onClick.Invoke();
        }

        callbackEnabled = true;
    }
}
