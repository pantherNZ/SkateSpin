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
    [SerializeField] GameObject infoPanel = null;
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
        public int numEntries;
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
        infoPanel.SetActive( false );
        trickSelector = FindObjectOfType<TrickSelectorPage>();

        restrictionDropDown.onValueChanged.AddListener( ( x ) => FilterEntries( true, false ) );
        filter.onValueChanged.AddListener( ( x ) => FilterEntries( false, true ) );
    }

    public void OnEventReceived( IBaseEvent e )
    {
        if( e.GetType() == typeof( DataLoadedEvent ) )
            Initialise();
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
                texts[0].text = string.Format( "Difficulty - {0} ({1})",
                        difficulty,
                        DataHandler.Instance.DifficultyNames[difficulty] );
                buttons.Add( difficultyHeading.GetComponent<Button>() );

                var newDifficultyEntry = new DifficultyData()
                {
                    uiElement = difficultyHeading,
                    text = texts[1],
                    button = buttons.Back(),
                    numEntries = perDifficultyData[difficulty].Count,
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

                        var before = trick.status;
                        trick.status = ( DataHandler.TrickEntry.Status )( ( ( int )trick.status + 1 ) % ( int )DataHandler.TrickEntry.Status.MaxStatusValues );

                        if( before == DataHandler.TrickEntry.Status.Landed || trick.status == DataHandler.TrickEntry.Status.Landed )
                        {
                            trickSelector.SetLandedDataDirty();
                            RecalculateCompletionPercentages( false );
                        }

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

            var categoryEndIdx = verticalLayout.transform.childCount;

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
    }

    public void FilterEntries( bool updateRestriction, bool updateFilter )
    {
        var restrictionOption = restrictionDropDown.options[restrictionDropDown.value].text;
        var filterLowercase = filter.text.ToLower();
        bool filterLanded = restrictionOption == "Complete Tricks";
        bool filterBanned = restrictionOption == "Banned Tricks";
        bool filterUnlanded = restrictionOption == "Incomplete Tricks";

        if( ( filterLowercase.Length > 0 || restrictionDropDown.value != 0 ) && !difficultyEntryData.Any( ( data ) => data.Value.isOpen ) )
            CollapseOrExpandAllEntries( false );

        foreach( var (category, data) in difficultyEntryData )
        {
            data.anyChildVisible = false;

            foreach( var (difficulty, diffEntry) in Utility.Enumerate( data.perDifficultyData ) )
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
                diffEntry.uiElement.SetActive( data.isOpen && diffEntry.isOpen && anyTrickVisible );
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
                if( collapse && data.perDifficultyData[i].isOpen )
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
                var completionPercent = ( ( float )completionData[difficulty + 1].First ).SafeDivide( ( float )completionData[difficulty + 1].Second );
                DifficultyEntry.text.text = Mathf.RoundToInt( completionPercent * 100.0f ).ToString() + "%";

                if( updateTrickEntryVisuals )
                    foreach( var trickEntry in DifficultyEntry.tricks )
                        UpdateTrickEntryVisual( trickEntry );
            }
        }
    }

    public override void OnShown()
    {
        FilterEntries( true, true );
        RecalculateCompletionPercentages( true );
    }

    private void UpdateTrickEntryVisual( TrickData trick )
    {
        var strikethrough = trick.text.GetComponentInChildren<Image>();
        strikethrough.color = strikethrough.color.SetA( trick.entry.status == DataHandler.TrickEntry.Status.Landed ? 1.0f : 0.0f );
        trick.text.color = 
            trick.entry.status == DataHandler.TrickEntry.Status.Landed ? new Color( 1.0f, 93.0f / 255.0f, 93.0f / 255.0f ) :
            trick.entry.status == DataHandler.TrickEntry.Status.Banned ? new Color( 0.5f, 0.5f, 0.5f, 0.75f ) :
            Color.white;
        strikethrough.transform.localScale = strikethrough.transform.localScale.SetX( GetTextWidth( trick.text ) / 100.0f );
    }

    int GetTextWidth( Text text )
    {
        int totalLength = 0;

        Font font = text.font; //text is my UI text
        char[] arr = text.text.ToCharArray();

        foreach( char c in arr )
        {
            font.RequestCharactersInTexture( c.ToString(), text.fontSize, text.fontStyle );
            font.GetCharacterInfo( c, out var characterInfo, text.fontSize );
            totalLength += characterInfo.advance;
        }

        return totalLength;
    }

    public void ExpandCategory( string category )
    {
        CollapseOrExpandAllEntries( false );
        difficultyEntryData[category].categoryEntry.onClick.Invoke();
    }

    public void ToggleInfoPanel()
    {
        infoPanel.ToggleActive();
    }
}