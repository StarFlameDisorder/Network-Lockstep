cmake_minimum_required(VERSION 4.1)
project(Server)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

set(CMAKE_PREFIX_PATH "D:/ProgramFiles/Qt/6.10.2/mingw_64")

find_package(Qt6 COMPONENTS
        Core
        Gui
        Widgets
        Network
        REQUIRED)

include_directories(${CMAKE_CURRENT_SOURCE_DIR}/protobuf/include)

add_executable(Server main.cpp
        TcpServer.cpp
        TcpServer.h
        LoggerStream.cpp
        LoggerStream.h
        NetworkDispatcher.cpp
        NetworkDispatcher.h
        GameServer.cpp
        GameServer.h
        UdpServer.cpp
        UdpServer.h
        protobuf/output/test.pb.cc
        protobuf/output/test.pb.h
        protobuf/output/SyncMessage.pb.cc
        protobuf/output/SyncMessage.pb.h
        protobuf/output/ConnectMessage.pb.cc
        protobuf/output/ConnectMessage.pb.h
        protobuf/output/GameMessage.pb.cc
        protobuf/output/GameMessage.pb.h
        protobuf/output/UnityMath.pb.cc
        protobuf/output/UnityMath.pb.h
)


target_link_directories(Server PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/protobuf/lib)

target_link_libraries(Server
        Qt::Core
        Qt::Gui
        Qt::Widgets
        Qt::Network
        protobuf
)

if (WIN32 AND NOT DEFINED CMAKE_TOOLCHAIN_FILE)
    set(DEBUG_SUFFIX)
    if (MSVC AND CMAKE_BUILD_TYPE MATCHES "Debug")
        set(DEBUG_SUFFIX "d")
    endif ()
    set(QT_INSTALL_PATH "${CMAKE_PREFIX_PATH}")
    if (NOT EXISTS "${QT_INSTALL_PATH}/bin")
        set(QT_INSTALL_PATH "${QT_INSTALL_PATH}/..")
        if (NOT EXISTS "${QT_INSTALL_PATH}/bin")
            set(QT_INSTALL_PATH "${QT_INSTALL_PATH}/..")
        endif ()
    endif ()
    if (EXISTS "${QT_INSTALL_PATH}/plugins/platforms/qwindows${DEBUG_SUFFIX}.dll")
        add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E make_directory
                "$<TARGET_FILE_DIR:${PROJECT_NAME}>/plugins/platforms/")
        add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy
                "${QT_INSTALL_PATH}/plugins/platforms/qwindows${DEBUG_SUFFIX}.dll"
                "$<TARGET_FILE_DIR:${PROJECT_NAME}>/plugins/platforms/")
    endif ()
    foreach (QT_LIB Core Gui Widgets)
        add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy
                "${QT_INSTALL_PATH}/bin/Qt6${QT_LIB}${DEBUG_SUFFIX}.dll"
                "$<TARGET_FILE_DIR:${PROJECT_NAME}>")
    endforeach (QT_LIB)
    add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy
            "${CMAKE_CURRENT_SOURCE_DIR}/protobuf/lib/libprotobuf.dll"
            "$<TARGET_FILE_DIR:${PROJECT_NAME}>")
endif ()