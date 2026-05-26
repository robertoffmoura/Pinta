//
// ShortcutEditDialog.cs
//

using System;
using Pinta.Core;
using Gtk;

namespace Pinta.Actions;

internal sealed class ShortcutEditDialog : Adw.MessageDialog
{
	private readonly Gtk.ShortcutLabel shortcut_label;
	private readonly string original_shortcut;
	
	// The results that will be saved if the user accepts.
	public string? ResultAccelerator { get; private set; }
	public uint? ResultKeyval { get; private set; }

	// An event fired when the user selects a key, but before saving.
	// Used so the parent window can update the UI instantly.
	public event EventHandler? ShortcutCaptured;

	public ShortcutEditDialog (Window parent, string title, string currentShortcut)
	{
		TransientFor = parent;
		Heading = title;
		Body = Translations.GetString ("Press a new key combination to assign to this action.");

		original_shortcut = currentShortcut;

		// Create the UI to show the key being pressed
		var box = Gtk.Box.New (Gtk.Orientation.Vertical, 12);
		box.MarginTop = 12;
		box.MarginBottom = 12;
		box.Halign = Gtk.Align.Center;

		shortcut_label = Gtk.ShortcutLabel.New (currentShortcut);
		shortcut_label.DisabledText = Translations.GetString ("Waiting for input...");
		box.Append (shortcut_label);

		ExtraChild = box;

		// Buttons
		AddResponse ("cancel", Translations.GetString ("Cancel"));
		AddResponse ("reset", Translations.GetString ("Reset to Default"));
		AddResponse ("save", Translations.GetString ("Save"));

		SetResponseAppearance ("save", Adw.ResponseAppearance.Suggested);
		SetResponseAppearance ("reset", Adw.ResponseAppearance.Destructive);

		SetDefaultResponse ("save");

		// Listen for keyboard input
		var key_controller = Gtk.EventControllerKey.New ();
		key_controller.OnKeyPressed += HandleKeyPressed;
		
		// Add the controller to the dialog itself so it catches everything while focused
		AddController (key_controller);
	}

	private bool HandleKeyPressed (Gtk.EventControllerKey controller, Gtk.EventControllerKey.KeyPressedSignalArgs args)
	{
		Gdk.Key key = args.GetKey ();
		Gdk.ModifierType mods = args.State;

		// Ignore standalone modifier key presses (e.g. just pressing 'Ctrl' or 'Shift' without a letter)
		if (key.IsControlKey () || key.Value == Gdk.Constants.KEY_Shift_L || key.Value == Gdk.Constants.KEY_Shift_R ||
		    key.Value == Gdk.Constants.KEY_Alt_L || key.Value == Gdk.Constants.KEY_Alt_R ||
		    key.Value == Gdk.Constants.KEY_Meta_L || key.Value == Gdk.Constants.KEY_Meta_R) {
			return false;
		}

		// Clean up the modifiers. We only care about Ctrl, Shift, and Alt.
		// GTK sometimes includes NumLock (Mod2) or CapsLock, which breaks accelerators.
		Gdk.ModifierType cleanMods = (Gdk.ModifierType)0;
		if (mods.HasFlag (Gdk.ModifierType.ControlMask)) cleanMods |= Gdk.ModifierType.ControlMask;
		if (mods.HasFlag (Gdk.ModifierType.ShiftMask)) cleanMods |= Gdk.ModifierType.ShiftMask;
		if (mods.HasFlag (Gdk.ModifierType.AltMask)) cleanMods |= Gdk.ModifierType.AltMask;
		if (mods.HasFlag (Gdk.ModifierType.MetaMask)) cleanMods |= Gdk.ModifierType.MetaMask;

		// Gtk.Functions.AcceleratorName builds a standard string like "<Control><Shift>S"
		string accel_string = Gtk.Functions.AcceleratorName (key.Value, cleanMods);

		if (string.IsNullOrEmpty (accel_string))
			return false;

		// Normalise Mac's <Meta> back to <Primary> so it saves cross-platform natively
		bool isMac = SystemManager.GetOperatingSystem () == OS.Mac;
		if (isMac) {
			accel_string = accel_string.Replace ("<Meta>", "<Primary>");
		} else {
			accel_string = accel_string.Replace ("<Control>", "<Primary>");
		}

		ResultAccelerator = accel_string;
		ResultKeyval = key.Value;

		// Update the label instantly so the user sees what they pressed
		shortcut_label.Accelerator = accel_string.Replace ("<Primary>", isMac ? "<Meta>" : "<Control>");
		
		ShortcutCaptured?.Invoke (this, EventArgs.Empty);

		return true; // Stop event propagation
	}
}