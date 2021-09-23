using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PageNavigator : MonoBehaviour
{
    [SerializeField] private List<CanvasGroup> pages = new List<CanvasGroup>();
    [SerializeField] private RectTransform panel = null;
    [SerializeField] private float centreX = 0.0f;
    [SerializeField] private float moveTimeSec = 0.0f;
    private CanvasGroup canvasGroup;
    private float leftX;
    private int currentPage = 0;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.SetVisibility( false );

        GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        foreach( var page in pages )
        {
            page.SetVisibility( false );
            page.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        leftX = panel.anchoredPosition.x;
        Utility.FunctionTimer.CreateTimer( 0.01f, () => ShowPage( 0 ) );
        SwipeManager.OnSwipeDetected += OnSwipeDetected;
    }

    public void ToggleMenu()
    {
        if( !canvasGroup.blocksRaycasts )
            canvasGroup.SetVisibility( true );

        StartCoroutine( MovePanel( panel.anchoredPosition.x < centreX ? centreX : leftX ) );
    }

    private IEnumerator MovePanel( float xPos )
    {
        float speed = Mathf.Abs( xPos - panel.anchoredPosition.x ) / moveTimeSec;
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

        if( !canvasGroup.blocksRaycasts )
            canvasGroup.SetVisibility( true );
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

            page.SetVisibility( false );
        }

        if( pages[index].gameObject.TryGetComponent( out IBasePage handler ) )
            handler.OnShown();

        foreach( Transform child in pages[index].transform )
            if( child.TryGetComponent( out IBasePage childHandler ) )
                childHandler.OnShown();

        pages[index].SetVisibility( true );
        canvasGroup.SetVisibility( false );
        panel.anchoredPosition = panel.anchoredPosition.SetX( leftX );
        currentPage = index;
    }

    void OnSwipeDetected( Swipe direction, Vector2 swipeVelocity )
    {
        if( direction == Swipe.Left )
            ShowPage( ( currentPage - 1 + pages.Count ) % pages.Count );
        else if( direction == Swipe.Right )
            ShowPage( ( currentPage + 1 ) % pages.Count );
    }
}
