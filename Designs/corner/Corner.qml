import QtQuick
import QtQuick.Effects
import qs.Commons
import "../plugins/io.github.sirjul1337.lock-explorer/designs"

// Everything gathered into one corner and nothing anywhere else. The
// wallpaper is barely touched, the type is right aligned off the edge, and a
// hairline separates the clock from the sign-in.
DesignBase {
  id: lock
  inputItem: field.input

  readonly property int margin: Math.round(Math.min(width, height) * 0.09)
  readonly property int fieldWidth: 340

  Wallpaper { anchors.fill: parent; lock: lock; blur: 0.2; dim: 0.08; vignetteTop: 0.1; vignetteMiddle: 0.05; vignetteBottom: 0.5 }

  MouseArea {
    anchors.fill: parent
    hoverEnabled: true
    onClicked: { lock.wakeRequested(); lock.forcePasswordFocus() }
    onPositionChanged: lock.wakeRequested()
  }

  Column {
    anchors.right: parent.right
    anchors.bottom: parent.bottom
    anchors.rightMargin: lock.margin
    anchors.bottomMargin: lock.margin
    spacing: 20

    Text {
      anchors.right: parent.right
      text: lock.clock("HH:mm")
      color: Color.lock.text
      font.family: Style.font.family
      font.pixelSize: Math.round(Style.font.baseSize * 8)
      font.weight: Font.Light
      font.letterSpacing: -3
      lineHeight: 0.9
      layer.enabled: true
      layer.effect: MultiEffect { shadowEnabled: true; shadowColor: Qt.rgba(0, 0, 0, 0.5); shadowBlur: 0.9; shadowVerticalOffset: 3 }
    }

    Text {
      anchors.right: parent.right
      text: Qt.formatDate(lock.now, "dddd d MMMM").toUpperCase()
      color: lock.withAlpha(Color.lock.text, 0.8)
      font.family: Style.font.family
      font.pixelSize: Style.font.body
      font.letterSpacing: 6
      layer.enabled: true
      layer.effect: MultiEffect { shadowEnabled: true; shadowColor: Qt.rgba(0, 0, 0, 0.5); shadowBlur: 0.8 }
    }

    Rectangle {
      anchors.right: parent.right
      width: lock.fieldWidth
      height: 1
      color: lock.withAlpha(Color.lock.text, 0.25)
    }

    PasswordField {
      id: field
      lock: lock
      anchors.right: parent.right
      width: lock.fieldWidth
      height: 52
      radius: height / 2
      showLockGlyph: false
      placeholder: lock.greeting() + ", " + lock.userName
      color: lock.withAlpha(Color.lock.background, 0.6)
    }

    Text {
      anchors.right: parent.right
      opacity: lock.snapshotMode ? 0 : 1
      text: lock.errorState
        ? lock.failureMessage
        : (lock.fingerprintConfigured ? "TOUCH SENSOR OR TYPE" : "ENTER TO UNLOCK")
      textFormat: Text.PlainText
      color: lock.errorState ? Color.lock.textError : lock.withAlpha(Color.lock.text, 0.5)
      font.family: Style.font.family
      font.pixelSize: Style.font.caption
      font.letterSpacing: 4
    }
  }
}
