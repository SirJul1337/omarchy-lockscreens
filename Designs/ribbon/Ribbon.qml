// Ribbon — one band straight across the middle, wallpaper above and below.
import QtQuick
import qs.Commons
import "../plugins/io.github.sirjul1337.lock-explorer/designs"

DesignBase {
  id: lock
  inputItem: field.input

  readonly property int bandHeight: 132
  readonly property int pad: Math.round(Math.min(width, height) * 0.07)

  Wallpaper {
    anchors.fill: parent
    lock: lock
    blur: 0.0
    dim: 0.12
    vignetteTop: 0.3
    vignetteMiddle: 0.05
    vignetteBottom: 0.35
  }

  MouseArea {
    anchors.fill: parent
    hoverEnabled: true
    onClicked: { lock.wakeRequested(); lock.forcePasswordFocus() }
    onPositionChanged: lock.wakeRequested()
  }

  Rectangle {
    id: band
    anchors.left: parent.left
    anchors.right: parent.right
    anchors.verticalCenter: parent.verticalCenter
    height: lock.bandHeight
    color: lock.withAlpha(Color.lock.background, 0.82)

    Rectangle {
      anchors.top: parent.top
      width: parent.width
      height: 1
      color: lock.withAlpha(Color.lock.text, 0.14)
    }
    Rectangle {
      anchors.bottom: parent.bottom
      width: parent.width
      height: 1
      color: lock.withAlpha(Color.lock.text, 0.14)
    }

    // Left: the clock.
    Text {
      anchors.left: parent.left
      anchors.leftMargin: lock.pad
      anchors.verticalCenter: parent.verticalCenter
      text: lock.clock("HH:mm")
      color: Color.lock.text
      font.family: Style.font.family
      font.pixelSize: Math.round(Style.font.baseSize * 4.2)
      font.weight: Font.Light
      font.letterSpacing: -1
    }

    // Middle: who and when.
    Column {
      anchors.centerIn: parent
      spacing: 3
      Text {
        anchors.horizontalCenter: parent.horizontalCenter
        text: lock.greeting() + ", " + lock.userName
        color: Color.lock.text
        font.family: Style.font.family
        font.pixelSize: Style.font.display
        font.weight: Font.DemiBold
      }
      Text {
        anchors.horizontalCenter: parent.horizontalCenter
        text: lock.errorState ? lock.failureMessage
          : (lock.authenticatingPassword ? "Checking…"
          : Qt.formatDate(lock.now, "dddd d MMMM").toUpperCase())
        textFormat: Text.PlainText
        color: lock.errorState ? Color.lock.textError : lock.withAlpha(Color.lock.text, 0.55)
        font.family: Style.font.family
        font.pixelSize: Style.font.caption
        font.letterSpacing: 5
      }
    }

    // Right: the field.
    PasswordField {
      id: field
      lock: lock
      anchors.right: parent.right
      anchors.rightMargin: lock.pad
      anchors.verticalCenter: parent.verticalCenter
      width: 340
      height: 50
      radius: height / 2
      outlineThickness: 1
      showLockGlyph: false
      placeholder: "Password"
      color: lock.withAlpha(Color.lock.text, 0.07)
    }
  }
}
