#include <gtk/gtk.h>

//How to compile on Linux.
// You need cmake and gtk: (Ubuntu/Debian/Mint)
//  sudo apt-get install libgtk-3-dev
//  sudo apt-get install cmake
//
/* Then Make a CMakeLists.txt file with this content:
cmake_minimum_required(VERSION 3.5)
project(MessageBox LANGUAGES C)

set(SRC_DIR "${CMAKE_CURRENT_SOURCE_DIR}")
set(SOURCES "${SRC_DIR}/messagebox.c")

add_library(messagebox SHARED ${SOURCES})

find_package(PkgConfig REQUIRED)
pkg_check_modules(GTK3 REQUIRED gtk+-3.0)
target_link_libraries(messagebox PUBLIC ${GTK3_LIBRARIES})
target_include_directories(messagebox PRIVATE ${GTK3_INCLUDE_DIRS})
*/
// Put the messagebox.c and CMakeLists.txt into a folder
// 
// Then open a terminal in that folder and write these commands:
// mkdir build
// cd build
// cmake ..
// make

// Function to handle the response from the dialog
void on_response(GtkDialog *dialog, gint response_id, gpointer user_data) {
    gtk_widget_destroy(GTK_WIDGET(dialog));
    gtk_main_quit();
}

void show_message_box(const char* message, const char* title) {
    GtkWidget *dialog;

    // Initialize the GTK library
    gtk_init(0, NULL);

    dialog = gtk_message_dialog_new(NULL, GTK_DIALOG_DESTROY_WITH_PARENT, GTK_MESSAGE_ERROR, GTK_BUTTONS_OK, "%s", message);
    gtk_window_set_title(GTK_WINDOW(dialog), title);

    // Connect the "response" signal of the dialog to the on_response function
    g_signal_connect(dialog, "response", G_CALLBACK(on_response), NULL);

    gtk_widget_show_all(dialog);

    // Enter the GTK main loop
    gtk_main();
}
