using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PageNavigator : MonoBehaviour, IEventReceiver
{
    [SerializeField] private List<GameObject> pages = new List<GameObject>();
    [SerializeField] private GameObject PageNavigationCanvas = null;
    [SerializeField] private RectTransform panel = null;
    [SerializeField] private float centreX = 0.0f;
    [SerializeField] private GameObject confirmDeleteDataPanel = null;
    [SerializeField] private RectTransform horizontalPageLayout = null;
    [SerializeField] private float offsetRoundingWidth = 100.0f;
    [SerializeField] private float pageMoveTimeSec = 1.0f;
    [SerializeField] private float navigatorMoveTimeSec = 0.0f;
    [SerializeField] private int dragPriority = 0;

    Vector2? dragPos;
    float start_x;
    private const float screenWidth = 1080.0f;
    private float leftX;
    private int currentPage = 0;

    void Start()
    {
        PageNavigationCanvas.SetActive( false );
        confirmDeleteDataPanel.SetActive( false );

        leftX = panel.anchoredPosition.x;
        Utility.FunctionTimer.CreateTimer( 0.01f, () => ShowPage( 0 ) );

        EventSystem.Instance.AddSubscriber( this );
    }

    public void ToggleMenu()
    {
        if( !PageNavigationCanvas.activeSelf )
            PageNavigationCanvas.SetActive( true );
        confirmDeleteDataPanel.SetActive( false );

        StartCoroutine( MovePanel( panel.anchoredPosition.x < centreX ? centreX : leftX ) );
    }

    private IEnumerator MovePanel( float xPos )
    {
        float speed = Mathf.Abs( xPos - panel.anchoredPosition.x ) / navigatorMoveTimeSec;
        float direction = Mathf.Sign( xPos - panel.anchoredPosition.x );

        while( ( direction > 0 && panel.anchoredPosition.x < xPos ) ||
               ( direction < 0 && panel.anchoredPosition.x > xPos ) )
        {
            var difference = Time.deltaTime * speed;
            difference = Mathf.Min( difference, Mathf.Abs( xPos - panel.anchoredPosition.x ) );
            panel.anchoredPosition = panel.anchoredPosition.SetX( panel.anchoredPosition.x + difference * direction );
            yield return null;
        }

        panel.anchoredPosition = panel.anchoredPosition.SetX( xPos );

        if( PageNavigationCanvas.activeSelf && direction == -1 )
            PageNavigationCanvas.SetActive( false );
    }

    public void ShowPage( int index )
    {
        foreach( var page in pages )
        {
            if( page == pages[index] )
                continue;

            if( page.gameObject.TryGetComponent( out IBasePage pageHandler ) )
                pageHandler.OnHidden();

            foreach( Transform child in page.transform )
                if( child.TryGetComponent( out IBasePage childHandler ) )
                    childHandler.OnHidden();
        }

        //pages[index].SetActive( true );
        PageNavigationCanvas.SetActive( false );

        if( pages[index].gameObject.TryGetComponent( out IBasePage handler ) )
            handler.OnShown();

        foreach( Transform child in pages[index].transform )
            if( child.TryGetComponent( out IBasePage childHandler ) )
                childHandler.OnShown();

        panel.anchoredPosition = panel.anchoredPosition.SetX( leftX );
        currentPage = index;
    }

    public void ToggleActive( GameObject obj )
    {
        obj.ToggleActive();
    }

    private bool disableDragThisFrame;

    private void Update()
    {
        if( dragPos != null )
        {
            float val = horizontalPageLayout.anchoredPosition.x + Input.mousePosition.x - dragPos.Value.x;
            horizontalPageLayout.anchoredPosition = horizontalPageLayout.anchoredPosition.SetX( Mathf.Clamp( val, start_x - screenWidth, start_x + screenWidth ) );
            dragPos = Input.mousePosition;
        }

        if( !disableDragThisFrame && Input.GetMouseButtonDown( 0 ) )
        {
            dragPos = Input.mousePosition;
            start_x = screenWidth * Mathf.Floor( horizontalPageLayout.anchoredPosition.x / screenWidth + 0.5f );
        }
        else if( Input.GetMouseButtonUp( 0 ) )
        {
            FinishDrag();
        }

        disableDragThisFrame = false;
    }

    private void FinishDrag()
    {
        if( Mathf.Abs( start_x - horizontalPageLayout.anchoredPosition.x ) < offsetRoundingWidth )
            StartCoroutine( MovePage( start_x ) );
        else
            StartCoroutine( MovePage( horizontalPageLayout.anchoredPosition.x < start_x ? start_x - screenWidth : start_x + screenWidth ) );

        dragPos = null;
    }

    private IEnumerator MovePage( float xPos )
    {
        float speed = Mathf.Abs( xPos - horizontalPageLayout.anchoredPosition.x ) / pageMoveTimeSec;
        float direction = Mathf.Sign( xPos - horizontalPageLayout.anchoredPosition.x );

        while( ( direction > 0 && horizontalPageLayout.anchoredPosition.x < xPos ) ||
               ( direction < 0 && horizontalPageLayout.anchoredPosition.x > xPos ) )
        {
            var difference = Time.deltaTime * speed;
            difference = Mathf.Min( difference, Mathf.Abs( xPos - horizontalPageLayout.anchoredPosition.x ) );
            horizontalPageLayout.anchoredPosition = horizontalPageLayout.anchoredPosition.SetX( horizontalPageLayout.anchoredPosition.x + difference * direction );
            yield return null;
        }

        horizontalPageLayout.anchoredPosition = horizontalPageLayout.anchoredPosition.SetX( xPos );
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
       if( e is DragStartedEvent dragEvent )
       {
            if( dragEvent.priority > dragPriority )
            {
                disableDragThisFrame = true;
                FinishDrag();
            }
       }
    }
}
