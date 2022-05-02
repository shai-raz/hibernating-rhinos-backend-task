import telnetlib
from time import sleep
import string
import random

HOST = "localhost"
PORT = 10011


def get_random_string(length: int):
    return ''.join(random.choice(string.ascii_letters) for _ in range(length))


def write_to_telnet(tn: telnetlib.Telnet, command):
    print(command.strip())
    tn.write((command).encode('UTF-8'))


def write_line_to_telnet(tn: telnetlib.Telnet, command):
    write_to_telnet(tn, command + "\r\n")


def set_key(tn: telnetlib.Telnet, key: str, value: str):
    write_line_to_telnet(tn, f"set {key} {len(value)}")
    sleep(0.0001)
    write_to_telnet(tn, value)
    return tn.read_until("\r\n".encode('UTF-8')).decode('UTF-8').strip()


def get_key(tn: telnetlib.Telnet, key: str):
    write_line_to_telnet(tn, f"get {key}")
    result = tn.read_until("\r\n".encode('UTF-8'))
    result += tn.read_very_eager()
    return result.decode('UTF-8')


def random_set_get(tn: telnetlib.Telnet):
    key = get_random_string(20)
    value = get_random_string(1024)

    print(set_key(tn, key, value))

    gk = get_key(tn, key)
    print(gk)

    assert value == gk.split("\r\n")[1]


tn = telnetlib.Telnet(HOST, PORT)

for i in range(10000):
    random_set_get(tn)