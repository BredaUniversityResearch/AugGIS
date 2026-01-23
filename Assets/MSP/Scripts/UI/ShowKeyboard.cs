using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowKeyboard : MonoBehaviour
{
    private TouchScreenKeyboard keyboard;

    public void ShowTheKeyboard(string text)
    {
        keyboard = TouchScreenKeyboard.Open(text, TouchScreenKeyboardType.Default);
    }
}
