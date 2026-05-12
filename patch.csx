using Desk42.BSM;
using System.IO;
using System.Text.RegularExpressions;

var path = "c:\\Users\\jacob\\Desk 42\\Assets\\_Project\\Scripts\\BSM\\ClientStateMachine.cs";
var text = File.ReadAllText(path);

// We need to fix the logic:
// if (injectedState == null) return InjectionResult.BlockedByCurrentState;
// Should be changed to only block if it's an unrecognized card, or handle the null gracefully.

var blockCode = "var injectedState = CreateInjectedState(cardType, targetState, overrideDuration);\n            if (injectedState == null) return InjectionResult.BlockedByCurrentState;\n\n            // Pause base BT, push injection\n            _baseBT.Pause();\n            _stateStack.Push(injectedState, _context);";

var fixCode = "var injectedState = CreateInjectedState(cardType, targetState, overrideDuration);\n\n            // If it's an injected state card, push it\n            if (injectedState != null)\n            {\n                _baseBT.Pause();\n                _stateStack.Push(injectedState, _context);\n            }\n            else if (targetState == _currentMoodState)\n            {\n                // If it doesn't have an injected state AND doesn't change mood, it's blocked\n                return InjectionResult.BlockedByCurrentState;\n            }";

text = text.Replace(blockCode, fixCode);
File.WriteAllText(path, text);
System.Console.WriteLine("Fixed ClientStateMachine!");
