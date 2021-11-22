using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class TrickListPage : IBasePage, IEventReceiver
{
    [SerializeField] VerticalLayoutGroup verticalLayout = null;
    [SerializeField] GameObject categoryHeadingPrefab = null;
    [SerializeField] GameObject difficultyHeadingPrefab = null;
    [SerializeField] GameObject trickEntryPrefab = null;
    [SerializeField] Dropdown restrictionDropDown = null;
    [SerializeField] InputField filter = null;
    TrickSelectorPage trickSelector;

    public class TrickData
    {
        public DataHandler.TrickEntry entry;
        public GameObject uiElement;
        public Text text;
        public bool isVisibleFromRestriction;
        public bool isVisibleFromFilter;
    }

    public class DifficultyData
    {
        public bool wasOpen;
        public bool isOpen;
        public Text text;
        public Button button;
        public GameObject uiElement;
        public List<TrickData> tricks = new List<TrickData>();
    }

    class CategoryData
    {
        public Button categoryEntry;
        public bool isOpen;
        public bool anyChildVisible;
        public List<DifficultyData> perDifficultyData = new List<DifficultyData>();
    }

    readonly Dictionary<string, CategoryData> difficultyEntryData = new Dictionary<string, CategoryData>();

    private bool difficultyCallbackEnabled = true;

    private void Awake()
    {
        EventSystem.Instance.AddSubscriber( this );
        trickSelector = FindObjectOfType<TrickSelectorPage>();

        restrictionDropDown.onValueChanged.AddListener( ( x ) => FilterEntries( true, false ) );
        filter.onValueChanged.AddListener( ( x ) => FilterEntries( false, true ) );
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is DataLoadedEvent || e is ResetSaveDataEvent )
        {
            Initialise();
        }
        else if( e is TrickLandedEvent || e is TrickDifficultyChangedEvent )
         {
            if( e is TrickDifficultyChangedEvent trickDiffChanged )
            {
                var trickData = difficultyEntryData[trickDiffChanged.trick.category]
                    .perDifficultyData[trickDiffChanged.previousDifficulty - 1]
                    .tricks.RemoveAndGet( 
                        ( x ) => x.entry == trickDiffChanged.trick 
                    );

                var newDifficultyData = difficultyEntryData[trickDiffChanged.trick.category].perDifficultyData[trickDiffChanged.trick.difficulty - 1];
                newDifficultyData.tricks.Add( trickData );

                trickData.uiElement.transform.SetParent( verticalLayout.transform );
                trickData.uiElement.transform.SetSiblingIndex( newDifficultyData.uiElement.transform.GetSiblingIndex() + 1 );
            }

            if( restrictionDropDown.value == 0 || restrictionDropDown.options[restrictionDropDown.value].text == "Banned" )
                FilterEntries( true, false );
            RecalculateCompletionPercentages( true );
        }
    }

    private void Initialise()
    {
        foreach( Transform child in verticalLayout.transform )
            child.gameObject.Destroy();

        foreach( var category in DataHandler.Instance.Categories )
        {
            var perDifficultyData = DataHandler.Instance.TrickData[category];

            var categoryHeading = Instantiate( categoryHeadingPrefab );
            categoryHeading.transform.SetParent( verticalLayout.transform, false );
            categoryHeading.GetComponentInChildren<Text>().text = category;
            var categoryButton = categoryHeading.GetComponent<Button>();

            var categoryInfo = difficultyEntryData.GetOrAdd( category );
            categoryInfo.categoryEntry = categoryButton;
            var buttons = new List<Button>();

            foreach( var (difficulty, name) in DataHandler.Instance.DifficultyNames )
            {
                var difficultyHeading = Instantiate( difficultyHeadingPrefab );
                difficultyHeading.SetActive( false );
                difficultyHeading.transform.SetParent( verticalLayout.transform, false );

                var texts = difficultyHeading.GetComponentsInChildren<Text>();
                texts[0].text = string.Format( "Difficulty - {0} ({1})", difficulty, DataHandler.Instance.DifficultyNames[difficulty] );
                buttons.Add( difficultyHeading.GetComponent<Button>() );

                var newDifficultyEntry = new DifficultyData()
                {
                    uiElement = difficultyHeading,
                    text = texts[1],
                    button = buttons.Back(),
                    wasOpen = true,
                };

                // Create trick entries
                foreach( var trick in perDifficultyData[difficulty] )
                {
                    var trickUIEntry = Instantiate( trickEntryPrefab );
                    trickUIEntry.SetActive( false );
                    trickUIEntry.transform.SetParent( verticalLayout.transform, false );
                    var text = trickUIEntry.GetComponentInChildren<Text>();
                    text.text = trick.name;

                    var newEntry = new TrickData()
                    {
                        entry = trick,
                        text = text,
                        uiElement = trickUIEntry,
                    };
                    newDifficultyEntry.tricks.Add( newEntry );

                    trickUIEntry.GetComponent<Button>().onClick.AddListener( () =>
                    {
                        if( restrictionDropDown.value != 0 )
                            return;

                        var before = newEntry.entry.status;
                        var status = ( DataHandler.TrickEntry.Status )Utility.Mod( ( int )newEntry.entry.status + 1, ( int )DataHandler.TrickEntry.Status.MaxStatusValues );
                        trickSelector.SetTrickStatus( newEntry.entry, status, false, false );

                        if( before == DataHandler.TrickEntry.Status.Landed )
                            RecalculateCompletionPercentages( false );

                        UpdateTrickEntryVisual( newEntry );
                    } );
                }

                categoryInfo.perDifficultyData.Add( newDifficultyEntry );

                // Setup on click for the difficulty entry
                buttons.Back().onClick.AddListener( () =>
                {
                    if( difficultyCallbackEnabled )
                        newDifficultyEntry.wasOpen = !newDifficultyEntry.wasOpen;

                    newDifficultyEntry.isOpen = !newDifficultyEntry.isOpen;
                    foreach( var trick in newDifficultyEntry.tricks )
                        trick.uiElement.SetActive( newDifficultyEntry.isOpen && trick.isVisibleFromRestriction && trick.isVisibleFromFilter );
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
                    difficultyCallbackEnabled = false;

                    foreach( var (difficultyData, button) in Utility.Zip( categoryInfo.perDifficultyData, buttons ) )
                        if( difficultyData.wasOpen )
                            button.onClick.Invoke();

                    difficultyCallbackEnabled = true;
                }

                foreach( var diffEntry in categoryInfo.perDifficultyData )
                {
                    bool anyTrickVisible = false;
                    bool categoryAndDifficultyOpen = ( diffEntry.isOpen || diffEntry.wasOpen ) && categoryInfo.isOpen;

                    foreach( var trick in diffEntry.tricks )
                    {
                        bool isVisible = trick.isVisibleFromRestriction && trick.isVisibleFromFilter;
                        anyTrickVisible |= isVisible;
                        trick.uiElement.SetActive( categoryAndDifficultyOpen && isVisible );
                    }

                    diffEntry.uiElement.SetActive( categoryInfo.isOpen && anyTrickVisible );
                    diffEntry.isOpen = categoryAndDifficultyOpen;
                }
            } );
        }

        FilterEntries( true, true );
        RecalculateCompletionPercentages( true );
    }

    public void FilterEntries( bool updateRestriction, bool updateFilter )
    {
        var restrictionOption = restrictionDropDown.options[restrictionDropDown.value].text;
        var filterLowercase = filter.text.ToLower();
        bool filterLanded = restrictionOption == "Complete";
        bool filterBanned = restrictionOption == "Banned";
        bool filterUnlanded = restrictionOption == "Incomplete";
        bool filterOrRestrictionActive = restrictionDropDown.value != 0 || filterLowercase.Length > 0;

        if( filterOrRestrictionActive && !difficultyEntryData.Any( ( data ) => data.Value.isOpen ) )
            CollapseOrExpandAllEntries( false );

        foreach( var( category, data ) in difficultyEntryData )
        {
            data.anyChildVisible = false;

            foreach( var( difficulty, diffEntry ) in Utility.Enumerate( data.perDifficultyData ) )
            {
                bool anyTrickVisible = false;

                foreach( var trickEntry in diffEntry.tricks )
                {
                    if( updateRestriction )
                        trickEntry.isVisibleFromRestriction = ( filterBanned && trickEntry.entry.status == DataHandler.TrickEntry.Status.Banned )
                            || ( filterLanded && trickEntry.entry.status == DataHandler.TrickEntry.Status.Landed )
                            || ( filterUnlanded && trickEntry.entry.status == DataHandler.TrickEntry.Status.Default )
                            || restrictionDropDown.value == 0;

                    if( updateFilter )
                        trickEntry.isVisibleFromFilter = filter.text.Length == 0 || trickEntry.entry.name.ToLower().Contains( filterLowercase );

                    bool isVisible = trickEntry.isVisibleFromRestriction && trickEntry.isVisibleFromFilter;
                    anyTrickVisible |= isVisible;
                    trickEntry.uiElement.SetActive( diffEntry.isOpen && isVisible );
                }

                data.anyChildVisible |= anyTrickVisible;
                bool active = data.isOpen && ( anyTrickVisible || !filterOrRestrictionActive );
                diffEntry.uiElement.SetActive( active );
            }
        }
    }

    public void ToggleAllEntries()
    {
        CollapseOrExpandAllEntries( difficultyEntryData.Any( ( data ) => data.Value.isOpen ) );
    }

    public void CollapseOrExpandAllEntries( bool collapse )
    {
        difficultyCallbackEnabled = false;

        foreach( var (_, data) in difficultyEntryData )
        {
            for( int i = 0; i < data.perDifficultyData.Count; ++i )
            {
                data.perDifficultyData[i].wasOpen = !collapse;
                if( data.perDifficultyData[i].isOpen == collapse )
                    data.perDifficultyData[i].button.onClick.Invoke();
            }

            if( data.isOpen == collapse )
                data.categoryEntry.onClick.Invoke();
        }

        difficultyCallbackEnabled = true;
    }

    public void RecalculateCompletionPercentages( bool updateTrickEntryVisuals )
    {
        var landedData = trickSelector.LandedData;

        foreach( var (category, data) in difficultyEntryData )
        {
            foreach( var (difficulty, DifficultyEntry) in Utility.Enumerate( data.perDifficultyData ) )
            {
                var completionData = landedData[category].perDifficultyLands;
                var completionPercent = ( ( float )completionData[difficulty + 1].First ).SafeDivide( completionData[difficulty + 1].Second );
                DifficultyEntry.text.text = Mathf.RoundToInt( completionPercent * 100.0f ).ToString() + "%";

                if( updateTrickEntryVisuals )
                    foreach( var trickEntry in DifficultyEntry.tricks )
                        UpdateTrickEntryVisual( trickEntry );
            }
        }
    }

    private void UpdateTrickEntryVisual( TrickData trick )
    {
        var strikethrough = trick.text.GetComponentInChildren<Image>();
        strikethrough.color = strikethrough.color.SetA( trick.entry.status == DataHandler.TrickEntry.Status.Landed ? 1.0f : 0.0f );
        trick.text.color = 
            trick.entry.status == DataHandler.TrickEntry.Status.Landed ? new Color( 1.0f, 93.0f / 255.0f, 93.0f / 255.0f ) :
            trick.entry.status == DataHandler.TrickEntry.Status.Banned ? new Color( 0.5f, 0.5f, 0.5f, 0.75f ) :
            Color.white;
        strikethrough.transform.localScale = strikethrough.transform.localScale.SetX( Utility.GetTextWidth( trick.text ) / 100.0f );
    }

    public void ExpandCategory( string category )
    {
        CollapseOrExpandAllEntries( true );
        difficultyEntryData[category].categoryEntry.onClick.Invoke();
    }
}