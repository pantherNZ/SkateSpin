using System.Collections;
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

        pages[0].SetVisibility( true );
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
            page.SetVisibility( false );

        pages[index].SetVisibility( true );
        HideMenu();
    }
}
