using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OperaMovieWnd : MonoBehaviour
{
    public void ShowMovieWnd()
    {
        WndManager.Instance.movieWnd.SetWndState(true);
    }
    public void HideMovieWnd()
    {
        WndManager.Instance.movieWnd.SetWndState(false);
    }
}
