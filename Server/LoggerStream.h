//
// Created by StarFlame on 2026/2/18.
//

#ifndef SERVER_LOGGER_H
#define SERVER_LOGGER_H
#include <QString>
#include <QTextStream>

enum LogLevel
{
    Debug,Info,Warning,Error
};

class LoggerStream
{
    public:
    explicit LoggerStream(LogLevel level=Debug);
    ~LoggerStream();

    LoggerStream& operator<<(const std::string& val) {
        m_stream << QString::fromStdString(val);
        return *this;
    }

    template <typename T>
    LoggerStream& operator<<(const T& val)
    {
        m_stream << val;
        return *this;
    }

    private:
    QString m_buf;
    QTextStream m_stream;
};

#define Debug() LoggerStream(LogLevel::Debug)
#define Info() LoggerStream(LogLevel::Info)
#define Warning() LoggerStream(LogLevel::Warning)
#define Error() LoggerStream(LogLevel::Error)

#endif //SERVER_LOGGER_H
