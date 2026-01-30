using System;
using System.Collections.Generic;
using UnityEngine;

internal class MainThreadDispatcher : MonoBehaviour {
    private static readonly Queue<Action> _actions = new();

    public static void Enqueue(Action action) {
        lock (_actions) {
            _actions.Enqueue(action);
        }
    }

    void Update() {
        lock (_actions) {
            while (_actions.Count > 0) {
                _actions.Dequeue()?.Invoke();
            }
        }
    }
}