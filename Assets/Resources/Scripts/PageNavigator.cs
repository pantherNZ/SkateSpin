using System.Collections.Generic;
using UnityEngine;

public class PageNavigator : MonoBehaviour
{
    [SerializeField] private List<CanvasGroup> pages = new List<CanvasGroup>();
    private CanvasGroup canvasGroup;

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

        Utility.FunctionTimer.CreateTimer( 0.01f, () => ShowPage( 0 ) );
    }

    public void ShowMenu()
    {
        canvasGroup.SetVisibility( true );
    }

    public void HideMenu()
    {
        canvasGroup.SetVisibility( false );
    }

    public void ShowPage( int index )
    {
        foreach( var page in pages )
        {
            page.SetVisibility( false );
            if( page.gameObject.TryGetComponent( out IBasePage handler ) )
                handler.OnHidden();
        }

        {
            pages[index].SetVisibility( true );
            if( pages[index].gameObject.TryGetComponent( out IBasePage handler ) )
                handler.OnShown();
        }

        HideMenu();
    }
}
