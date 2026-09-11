import QtQuick
import Quickshell.Io
import qs.Commons
import "../plugins/io.github.sirjul1337.lock-explorer/designs"

// A design that imports a module the gate does not allow. There is nothing
// harmful in the file itself -- the point is only that scan.py must refuse the
// import, and must still refuse it when the same pull request has edited
// scan.py to say yes.
DesignBase {
  id: lock

  Text {
    anchors.centerIn: parent
    text: lock.clock("HH:mm")
    color: Color.lock.text
    font.family: Style.font.family
    font.pixelSize: Style.font.display
  }
}
