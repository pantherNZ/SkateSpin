import sqlite3, json, glob

con = sqlite3.connect("../Assets/StreamingAssets/Database.db")
cur = con.cursor()

cur.execute("DROP TABLE IF EXISTS Tricks")
cur.execute("CREATE TABLE Tricks AS SELECT * FROM Tricks_Backup")

con.commit()
con.close()