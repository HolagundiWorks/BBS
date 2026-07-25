// Page.cpp — shared page utilities.
#include "Page.h"

namespace ui {

int nextControlId() {
    static int id = 1000;
    return ++id;
}

}  // namespace ui
