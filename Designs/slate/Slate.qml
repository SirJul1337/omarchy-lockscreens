// Slate — no wallpaper at all, just the theme's own colours.
import QtQuick
import qs.Commons
import "../plugins/io.github.sirjul1337.lock-explorer/designs"

DesignBase {
  id: lock
  inputItem: field.input

  readonly property int margin: Math.round(Math.min(width, height) * 0.12)

  // Deliberately no Wallpaper: the point of this one is the flat ground, so
  // it looks the same on every machine and changes only with the theme.
  Rectangle {
    anchors.fill: parent
    gradient: Gradient {
      GradientStop { position: 0.0; color: lock.withAlpha(Color.lock.background, 1) }
      GradientStop { position: 1.0; color: Qt.darker(lock.withAlpha(Color.lock.background, 1), 1.35) }
    }
  }

  MouseArea {
    anchors.fill: parent
    hoverEnabled: true
    onClicked: { lock.wakeRequested(); lock.forcePasswordFocus() }
    onPositionChanged: lock.wakeRequested()
  }

  Column {
    anchors.left: parent.left
    anchors.leftMargin: lock.margin
    anchors.verticalCenter: parent.verticalCenter
    spacing: 26

    Row {
      spacing: 16
      Rectangle {
        width: 4
        height: stack.implicitHeight
        radius: 2
        color: Color.lock.borderActive
      }
      Column {
        id: stack
        spacing: 2
        Text {
          text: lock.clock("HH:mm")
          color: Color.lock.text
          font.family: Style.font.family
          font.pixelSize: Math.round(Style.font.baseSize * 9)
          font.weight: Font.Light
          font.letterSpacing: -3
          lineHeight: 0.92
        }
        Text {
          text: Qt.formatDate(lock.now, "dddd d MMMM").toUpperCase()
          color: lock.withAlpha(Color.lock.text, 0.6)
          font.family: Style.font.family
          font.pixelSize: Style.font.body
          font.letterSpacing: 6
        }
      }
    }

    Column {
      spacing: 10

      Text {
        text: lock.greeting() + ", " + lock.userName
        color: lock.withAlpha(Color.lock.text, 0.85)
        font.family: Style.font.family
        font.pixelSize: Style.font.display
      }

      PasswordField {
        id: field
        lock: lock
        width: 380
        height: 54
        radius: Math.max(Style.cornerRadius, 10)
        outlineThickness: 1
        showLockGlyph: false
        textAlignment: TextInput.AlignLeft
        placeholder: "Password"
        color: lock.withAlpha(Color.lock.text, 0.06)
      }

      Text {
        opacity: lock.snapshotMode ? 0 : 1
        text: lock.errorState ? lock.failureMessage
          : (lock.authenticatingPassword ? "Checking…"
          : (lock.fingerprintConfigured ? "Enter to unlock, or touch the sensor" : "Enter to unlock"))
        textFormat: Text.PlainText
        color: lock.errorState ? Color.lock.textError : lock.withAlpha(Color.lock.text, 0.45)
        font.family: Style.font.family
        font.pixelSize: Style.font.bodySmall
      }
    }
  }

  Text {
    anchors.right: parent.right
    anchors.bottom: parent.bottom
    anchors.margins: lock.margin
    text: lock.hostName.toUpperCase()
    color: lock.withAlpha(Color.lock.text, 0.3)
    font.family: Style.font.family
    font.pixelSize: Style.font.caption
    font.letterSpacing: 6
  }
}
