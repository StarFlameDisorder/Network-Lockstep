//
// Created by StarFlame on 2026/3/28.
//

#ifndef SERVER_ROOMMANAGER_H
#define SERVER_ROOMMANAGER_H
#include <QHash>
#include <QObject>
#include "Room.h"

class RoomManager:public QObject
{
    Q_OBJECT
public:
    RoomManager(QObject* parent=nullptr);

    void addNewRoom();
    void findRoom(quint64 roomId);
    void joinRoom(quint64 roomId,quint64 playerId);
    void leaveRoom(quint64 roomId,quint64 playerId);


private:
    quint64 m_nextRoomId=1;
    QHash<quint64,Room> m_roomMap;
};


#endif //SERVER_ROOMMANAGER_H