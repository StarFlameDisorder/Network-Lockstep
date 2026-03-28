//
// Created by StarFlame on 2026/3/28. 玩家数据管理
//

#ifndef SERVER_PLAYERMANAGER_H
#define SERVER_PLAYERMANAGER_H
#include <QObject>
#include <QHash>

struct Player{
    Player(quint64 playerId,QString playerName):id{playerId},name(playerName)
    {}

    Player(){}

    quint64 id;
    quint64 clientId;
    QString name;

};

class PlayerManager:public QObject
{
    Q_OBJECT
public:
    PlayerManager(QObject* parent=nullptr);
    quint64 addPlayer(QString name,quint64 clientId);//玩家必须有名称
    Player &findPlayerById(quint64 playerId);
    quint64 findPlayer(QString name);
    quint64 findPlayer(quint64 clientId);
    void bindClientId(quint64 playerId,quint64 clientId);

private:
    quint64 m_nextPlayerId=1;
    QHash<QString,quint64> m_playersNameMap;
    QHash<quint64,quint64> m_clientIdMap;
    QHash<quint64,Player> m_playerMap;
};


#endif //SERVER_PLAYERMANAGER_H
